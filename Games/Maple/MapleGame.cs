using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Verdant.Games.Maple
{
    public class MapleGame
    {
        public NaverAccount Account;
        public string MapleId;
        public List<string> MapleIds;

        private string ngmPath;

        private HttpClient webClient => Account.WebClient;

        public MapleGame(NaverAccount account)
        {
            Account = account;
        }

        public async Task<bool> Init()
        {
            // Find NGM location
            using (var reg = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)) // 32bit reg
            using (RegistryKey key = reg.OpenSubKey("Software\\Nexon\\Shared", false))
            {
                if (key == null)
                    return false;

                ngmPath = key.GetValue(null).ToString() + @"\NGM\NGM.exe";
            }

            // Get front page, if no load, then channel and try again. If not then gg
            // NOTE: make sure if >1 maple id, there is one selected as default on the website!!
            // Or will not channel properly!!
            Debug.WriteLine("getting maple");
            if (!(await getCurrentMaple()))
            {
                Debug.WriteLine("have to channel");
                await channeling();

                Debug.WriteLine("channeled, getting again");
                if (!(await getCurrentMaple()))
                    return false;
            }

            return true;
        }


        // IE: "C:\ProgramData\Nexon\NGM\NGM.exe" -dll:platform.nexon.com/NGM/Bin/NGMDll.dll:1 -locale:KR -mode:launch -game:589825:0 -token:'MSGENC_cookie:45' -passarg:'WebStart' -timestamp:UNIX_TIMESTAMP -position:'GameWeb|http://maplestory.nexon.game.naver.com/MapleStory/Page/Optimize.aspx'
        private const string LAUNCH_LINE = "-dll:platform.nexon.com/NGM/Bin/NGMDll.dll:1 -locale:KR -mode:launch -game:589825:0 -token:'{0}:45' -passarg:'WebStart' -timestamp:{1} -position:'GameWeb|http://maplestory.nexon.game.naver.com/MapleStory/Page/Optimize.aspx'";
        public async Task Start()
        {
            string ts = ((Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds).ToString(); // timestamp: https://stackoverflow.com/questions/17632584/how-to-get-the-unix-timestamp-in-c-sharp
            // last minute cookies need to be get here - MSGENC
            await webClient.GetAsync("http://maplestory.nexon.game.naver.com/Common/A/B.aspx?_=" + ts);

            string msgenc = Account.Cookies.GetCookies(new Uri("http://maplestory.nexon.game.naver.com"))["MSGENC"].Value;
            string args = String.Format(LAUNCH_LINE, msgenc, ts);
            var psi = new ProcessStartInfo(ngmPath, args);
            Process.Start(psi);
        }

        private async Task channeling()
        {
            await webClient.GetAsync("http://api.game.naver.com/js/jslib.nhn?gameId=P_PN000046"); // redirs a few times for cookies
            await webClient.GetAsync("http://maplestory.nexon.game.naver.com/MapleStory/Page/Gnx.aspx?URL=Home/Index"); // the home page redirects a few times too

            // this last one probably doesnt matter but we do it anyway
            //HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "http://maplestory.nexon.game.naver.com/MapleStory/Page/Gnx.aspx?URL=Home/Index");
            //req.Headers.Add("Referer", "http://nxgamechanneling.nexon.game.naver.com/");
            //await webClient.SendAsync(req);
        }

        private Regex mapleIdRegex = new Regex("__nxArgs\\.identity = '(.+?)';");
        private async Task<bool> getCurrentMaple()
        {
            HttpResponseMessage res = await webClient.GetAsync("http://maplestory.nexon.game.naver.com/MapleStory/Page/Gnx.aspx?URL=Home/Index");
            res.EnsureSuccessStatusCode();

            string data = await res.Content.ReadAsStringAsync();
            Match m = mapleIdRegex.Match(data);
            if (!m.Success)
                return false;

            MapleId = m.Groups[1].Value;
            return true;
        }

        private Regex mapleIdsRegex = new Regex("'>(.+?)<\\/label>");
        public async Task GetMapleIds()
        {
            HttpResponseMessage res = await webClient.GetAsync("http://maplestory.nexon.game.naver.com/MapleStory/Common/GetGameIdListByName.aspx");
            res.EnsureSuccessStatusCode();

            string data = await res.Content.ReadAsStringAsync();
            MatchCollection mc = mapleIdsRegex.Matches(data);
            MapleIds = new List<string>();
            foreach (Match m in mc)
                MapleIds.Add(m.Groups[1].Value);
        }

        public async Task SwitchMapleId(string mapleId)
        {
            var _postData = new Dictionary<string, string>
            {
                { "gid", mapleId }
            };
            var postData = new FormUrlEncodedContent(_postData);

            HttpRequestMessage req = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("http://maplestory.nexon.game.naver.com/MapleStory/Common/OtherGameIdLogin.aspx")
            };
            req.Content = postData;
            req.Headers.Add("Referer", "http://maplestory.nexon.game.naver.com/MapleStory/Page/GnxPopup.aspx?URL=Membership/PopOtherGameIdLogin");
            req.Headers.Add("X-Requested-With", "XMLHttpRequest");
            HttpResponseMessage res = await webClient.SendAsync(req);
            res.EnsureSuccessStatusCode();

            await getCurrentMaple();
        }
    }
}
