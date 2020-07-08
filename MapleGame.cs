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
        private const string MAPLE_HOME = "https://maplestory.nexon.game.naver.com/Home/Main";
        private const string MAPLE_TESPIA_HOME = "https://maplestory.nexon.game.naver.com/Testworld/Main";

        public NaverAccount Account;
        public string MainCharName;
        public List<string> MapleIds;
        public string CharacterImageUrl = null;

        private string ngmPath;
        private string launchWID;
        private string tespiaWID;

        private HttpClient webClient => Account.WebClient;

        public MapleGame(NaverAccount account)
        {
            Account = account;
        }

        public async Task Init()
        {
            // Find NGM location
            ngmPath = Tools.GetNgmPath();
            if (ngmPath == null)
                throw new VerdantException.GameNotFoundException();

            // Get front page, if no load, then channel and try again. If not then gg
            // NOTE: make sure if >1 maple id, there is one selected as default on the website!!
            // Or will not channel properly!!
            Debug.WriteLine("getting maple");

            // preload - ensure everything is smooth with 2x get (loginproc on second if no good) + cheeky channel
            if (Account.Preloaded)
            {
                Debug.WriteLine("preload get");
                await webClient.GetAsync(MAPLE_HOME);
                Debug.WriteLine("pre 1");
                await webClient.GetAsync("http://api.game.naver.com/js/jslib.nhn?gameId=P_PN000046");
                Debug.WriteLine("pre 2");
                await webClient.GetAsync(MAPLE_HOME);
                Debug.WriteLine("pre ok");
                Account.SaveCookies();
            }

            if (!(await getCurrentMaple()))
                throw new VerdantException.ChannelingRequiredException();
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
        private const string TESPIA_LAUNCH_LINE = "-dll:platform.nexon.com/NGM/Bin/NGMDll.dll:1 -locale:KR -mode:launch -game:589826:0 -token:'{0}:{1}' -passarg:'WebStart' -timestamp:{2} -position:'GameWeb|https://maplestory.nexon.game.naver.com/Testworld/Main' -service:6 -architectureplatform:'none'";
        public async Task Start(bool tespia = false, bool firstTry = true)
        {
            // last minute cookies need to be get here - MSGENC and what not
            int ts = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds; // timestamp: https://stackoverflow.com/questions/17632584/how-to-get-the-unix-timestamp-in-c-sharp

            // last min update sesh
            Debug.WriteLine("updating session");
            var sesRes = await webClient.GetAsync("https://sso.nexon.game.naver.com/Ajax/Default.aspx?_vb=UpdateSession");
            string sesData = await sesRes.Content.ReadAsStringAsync();
            Debug.WriteLine(sesData);
            if (!sesData.Contains("name=\"ErrorCode\" value=\"0\""))
            {
                if (!firstTry)
                    throw new Exception("session error");

                // session update failed, implies loginProc to refresh NPP
                Debug.WriteLine("updateSess failed, should jslib + loginProc");
                await loginProc();
                // hard retry
                await Start(tespia, false);
                return;
            }

            Debug.WriteLine("msgenc update");
            // msgenc
            // along with the "WID" (check the homepage js, its basically append :WID (number) to end of msgenc - we store this whenever just get homepage
            HttpRequestMessage req = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://maplestory.nexon.game.naver.com/authentication/swk" + (tespia ? "t" : "") + "?h="),
                Content = null
            };
            req.Headers.Add("Referer", tespia ? MAPLE_TESPIA_HOME : MAPLE_HOME);
            req.Headers.Add("X-Requested-With", "XMLHttpRequest");

            var res = await webClient.SendAsync(req);
            string data = await res.Content.ReadAsStringAsync();
            Debug.WriteLine(data);
            if (!data.Contains("\"Code\":" + (tespia ? "0" : "1"))) // for some reason swkt code 0 for success
            {
                // the hell hole
                // there is one last fix - reselect maple id.
                throw new VerdantException.NoAuthException();
            }

            string msgenc = Account.Cookies.GetCookies(new Uri("http://maplestory.nexon.game.naver.com"))[tespia ? "MSGENCT" : "MSGENC"].Value;

            Debug.WriteLine("launching");
            string args = String.Format(tespia ? TESPIA_LAUNCH_LINE : LAUNCH_LINE, msgenc, tespia ? tespiaWID : launchWID, ts.ToString());
            var psi = new ProcessStartInfo(ngmPath, args);
            Process.Start(psi);
        }

        private async Task channeling()
        {
            await webClient.GetAsync("http://api.game.naver.com/js/jslib.nhn?gameId=P_PN000046"); // redirs a few times for cookies
            await webClient.GetAsync(MAPLE_HOME); // the home page may redirect too
        }

        private async Task loginProc()
        {
            await webClient.GetAsync("http://nxgamechanneling.nexon.game.naver.com/login/loginproc.aspx?gamecode=589824");

            // get the home page & update the launch WID
            HttpRequestMessage req = new HttpRequestMessage() { RequestUri = new Uri(MAPLE_HOME) };
            req.Headers.Add("Referer", "http://nxgamechanneling.nexon.game.naver.com/login/loginproc.aspx?gamecode=589824");

            HttpResponseMessage res = await webClient.SendAsync(req);
            res.EnsureSuccessStatusCode();
            string data = await res.Content.ReadAsStringAsync();

            Match wid = launchWIDRegex.Match(data);
            if (!wid.Success)
            {
                Debug.WriteLine("couldnt update wid!! probably will be a web verification ingame fail!");
            }
            else
            {
                launchWID = wid.Groups[1].Value;
                Debug.WriteLine("wid update: " + launchWID);
            }

            Account.SaveCookies();
        }

        private Regex charRepRegex = new Regex("<dd class=\"login_id\">.*?>(.+?).</");
        private Regex launchWIDRegex = new Regex("\\.LaunchGame\\('(\\d+)'\\)");
        private Regex charImgRegex = new Regex("<img src=\"(.+?)\" alt=\"\uB300\uD45C");
        private async Task<bool> getCurrentMaple()
        {
            HttpResponseMessage res = await webClient.GetAsync(MAPLE_HOME);
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

            CharacterImageUrl = null;
            Match imgM = charImgRegex.Match(data);
            if (imgM.Success)
                CharacterImageUrl = imgM.Groups[1].Value.Replace("/180", "");

            Match switchM = mapleIdSwitchTokenRegex.Match(data);
            if (switchM.Success)
                mapleIdSwitchToken = switchM.Groups[1].Value;

            return true;
        }

        private Regex mapleIdsRegex = new Regex(">(.+?)<\\/a>");
        private Regex mapleIdSwitchTokenRegex = new Regex("name=\"__RequestVerificationToken\".*value=\"(.+?)\"");
        private string mapleIdSwitchToken = null;
        public async Task GetMapleIds()
        {
            HttpRequestMessage req = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://maplestory.nexon.game.naver.com/Authentication/Email/IDList")
            };
            req.Headers.Add("X-Requested-With", "XMLHttpRequest");
            var _postData = new Dictionary<string, string>
            {
                { "__RequestVerificationToken", mapleIdSwitchToken }
            };
            var postData = new FormUrlEncodedContent(_postData);
            req.Content = postData;

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
                { "redirectTo", MAPLE_HOME }
            };
            var postData = new FormUrlEncodedContent(_postData);

            HttpRequestMessage req = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://maplestory.nexon.game.naver.com/Authentication/Email/ChangeID")
            };
            req.Content = postData;
            req.Headers.Add("Referer", MAPLE_HOME);
            HttpResponseMessage res = await webClient.SendAsync(req);
            res.EnsureSuccessStatusCode();

            Account.SaveCookies();

            await getCurrentMaple();
        }

        public class TespiaNotEnabled : Exception { }

        public async Task UseTespia()
        {
            HttpResponseMessage res = await webClient.GetAsync(MAPLE_TESPIA_HOME);
            res.EnsureSuccessStatusCode();
            string data = await res.Content.ReadAsStringAsync();

            Match wid = launchWIDRegex.Match(data);
            if (!wid.Success)
                throw new TespiaNotEnabled();
            tespiaWID = wid.Groups[1].Value;
        }
    }
}
