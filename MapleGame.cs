using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Verdant
{
    public class MapleGame
    {
        public NaverAccount Account;
        public string MainCharName;
        public List<string> MapleIds;

        private string ngmPath;
        private string launchWID;

        private HttpClient webClient => Account.WebClient;

        public MapleGame(NaverAccount account)
        {
            Account = account;
        }

        public class MapleNotFoundException : Exception { }
        public class ChannelingRequiredException : Exception { }

        public async Task Init()
        {
            // Find NGM location
            using (var reg = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)) // 32bit reg
            using (RegistryKey key = reg.OpenSubKey("Software\\Nexon\\Shared", false))
            {
                if (key == null)
                    throw new MapleNotFoundException();

                ngmPath = key.GetValue(null).ToString() + @"\NGM\NGM.exe";
            }

            // Get front page, if no load, then channel and try again. If not then gg
            // NOTE: make sure if >1 maple id, there is one selected as default on the website!!
            // Or will not channel properly!!
            Debug.WriteLine("getting maple");
            if (!(await getCurrentMaple()))
                throw new ChannelingRequiredException();
        }

        public async Task Channel()
        {
            Debug.WriteLine("have to channel");
            await channeling();

            Debug.WriteLine("channeled, saving and getting again");
            Account.SaveCookies();

            if (!(await getCurrentMaple()))
                throw new Exception("no");
        }

        private const string LAUNCH_LINE = "-dll:platform.nexon.com/NGM/Bin/NGMDll.dll:1 -locale:KR -mode:launch -game:589825:0 -token:'{0}:{1}' -passarg:'WebStart' -timestamp:{2} -position:'GameWeb|https://maplestory.nexon.game.naver.com/' -service:6";
        public async Task Start()
        {
            // last minute cookies need to be get here - MSGENC and what not
            int ts = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds; // timestamp: https://stackoverflow.com/questions/17632584/how-to-get-the-unix-timestamp-in-c-sharp

            // last min update sesh
            Debug.WriteLine("updating session");
            var sesRes = await webClient.GetAsync("https://sso.nexon.game.naver.com/Ajax/Default.aspx?_vb=UpdateSession");
            Debug.WriteLine(await sesRes.Content.ReadAsStringAsync());

            Debug.WriteLine("msgenc update");
            // msgenc
            // along with the "WID" (check the homepage js, its basically append :WID (number) to end of msgenc - we store this whenever just get homepage
            HttpRequestMessage req = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://maplestory.nexon.game.naver.com/authentication/swk?h="),
                Content = null
            };
            req.Headers.Add("Referer", "http://maplestory.nexon.game.naver.com/");
            req.Headers.Add("X-Requested-With", "XMLHttpRequest");

            var res = await webClient.SendAsync(req);
            string data = await res.Content.ReadAsStringAsync();
            if (!data.Contains("\"Code\":1"))
                throw new Exception("could not auth for game...");

            string msgenc = Account.Cookies.GetCookies(new Uri("http://maplestory.nexon.game.naver.com"))["MSGENC"].Value;

            Debug.WriteLine("launching");
            string args = String.Format(LAUNCH_LINE, msgenc, launchWID, ts.ToString());
            var psi = new ProcessStartInfo(ngmPath, args);
            Process.Start(psi);
        }

        private async Task channeling()
        {
            await webClient.GetAsync("http://api.game.naver.com/js/jslib.nhn?gameId=P_PN000046"); // redirs a few times for cookies
            await webClient.GetAsync("http://maplestory.nexon.game.naver.com"); // the home page may redirect too
        }

        private Regex charRepRegex = new Regex("<dd class=\"login_id\"><a href=\".+?\" target=\"_blank\">(.+?)님<\\/a><\\/dd>");
        private Regex launchWIDRegex = new Regex("PLATFORM\\.LaunchGame\\('(\\d+)'\\)");
        private async Task<bool> getCurrentMaple()
        {
            HttpResponseMessage res = await webClient.GetAsync("http://maplestory.nexon.game.naver.com");
            res.EnsureSuccessStatusCode();

            string data = await res.Content.ReadAsStringAsync();
            if (!data.Contains("isLogin: true"))
                return false;

            Match wid = launchWIDRegex.Match(data);
            if (!wid.Success)
                return false;
            launchWID = wid.Groups[1].Value;
            Debug.WriteLine("wid: " + launchWID);

            Match m = charRepRegex.Match(data);
            MainCharName = "(Unknown)";
            if (m.Success)
                MainCharName = m.Groups[1].Value;
            return true;
        }

        private Regex mapleIdsRegex = new Regex(">(.+?)<\\/a>");
        public async Task GetMapleIds()
        {
            HttpRequestMessage req = new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri("https://maplestory.nexon.game.naver.com/Authentication/Email/IDList")
            };
            req.Headers.Add("Referer", "http://maplestory.nexon.game.naver.com/");
            req.Headers.Add("X-Requested-With", "XMLHttpRequest");

            HttpResponseMessage res = await webClient.SendAsync(req);
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
                { "id", mapleId },
                { "master", "0" },
                { "redirectTo", "https://maplestory.nexon.game.naver.com/" }
            };
            var postData = new FormUrlEncodedContent(_postData);

            HttpRequestMessage req = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://maplestory.nexon.game.naver.com/Authentication/Email/ChangeID")
            };
            req.Content = postData;
            req.Headers.Add("Referer", "http://maplestory.nexon.game.naver.com/");
            HttpResponseMessage res = await webClient.SendAsync(req);
            res.EnsureSuccessStatusCode();

            Account.SaveCookies();

            await getCurrentMaple();
        }
    }
}
