using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Verdant
{
    public class NaverAccount
    {
        private const string LOGIN_KEYS = "https://nid.naver.com/login/ext/keys.nhn";
        private const string LOGIN_URL = "https://nid.naver.com/nidlogin.login";
        private const string OTP_STATUS_URL = "https://nid.naver.com/user2/api/twoStepVerification/getStatus.nhn";
        private const string PROFILE_URL = "https://static.nid.naver.com/getProfile.nhn?svc=my";

        private HttpClientHandler WebClientHandler;
        private string otpData;
        private string pathToSavedCookies;

        public HttpClient WebClient;
        public NaverKeySet LoginKeys;
        public string Nickname;
        public string AvatarUrl;

        public CookieContainer Cookies
        {
            get { return WebClientHandler.CookieContainer; }
            set { WebClientHandler.CookieContainer = value; }
        }

        public bool WaitingOtp = false; // needs otp
        public bool WaitingCaptcha = false;
        public bool LoggedIn = false;
        public string CaptchaImageUrl => $"https://nid.naver.com/login/image/captcha/nhncaptchav4.gif?key={CaptchaKey}&1";
        public string CaptchaKey;

        public NaverAccount(string pathToSavedCookies = null)
        {
            this.pathToSavedCookies = pathToSavedCookies;
            var cc = new CookieContainer();
            if (pathToSavedCookies != null && File.Exists(pathToSavedCookies))
            {
                try
                {
                    using (Stream s = File.Open(pathToSavedCookies, FileMode.Open))
                    {
                        var f = new BinaryFormatter();
                        cc = (CookieContainer)f.Deserialize(s);
                    }
                }
                catch
                {
                    // corrupted?
                    File.Delete(pathToSavedCookies);
                }
            }

            WebClientHandler = new HttpClientHandler()
            {
                AllowAutoRedirect = true,
                UseCookies = true,
                CookieContainer = cc
            };

            WebClient = new HttpClient(WebClientHandler);

            WebClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; rv:11.0) like Gecko");
            WebClient.DefaultRequestHeaders.Add("DNT", "1");
            WebClient.DefaultRequestHeaders.Add("Accept-Language", "ko-KR");
        }

        public void SaveCookies()
        {
            if (pathToSavedCookies == null)
                return;

            using (Stream s = File.Create(pathToSavedCookies))
            {
                BinaryFormatter f = new BinaryFormatter();
                f.Serialize(s, Cookies);
            }
        }

        private async Task getKeys()
        {
            HttpResponseMessage res = await WebClient.GetAsync(LOGIN_KEYS);
            res.EnsureSuccessStatusCode();

            string[] data = (await res.Content.ReadAsStringAsync()).Split(',');
            LoginKeys = new NaverKeySet()
            {
                SessionKey = data[0],
                KeyName = data[1],
                EValue = data[3], // i think naver has this the wrong way round
                NValue = data[2]
            };
        }

        private RSACryptoServiceProvider loadKeysInRsa()
        {
            var rsa = new RSACryptoServiceProvider();

            // load key info
            RSAParameters rsaKey = new RSAParameters();
            rsaKey.Modulus = hexToByteArray(LoginKeys.NValue); // modulus = n
            rsaKey.Exponent = hexToByteArray(LoginKeys.EValue); // exponent = e

            rsa.ImportParameters(rsaKey);
            return rsa;
        }

        private string encryptIdWithRsa(RSACryptoServiceProvider rsa, string username, string password)
        {
            string payload =
                _lengthWithValue(LoginKeys.SessionKey) +
                _lengthWithValue(username) +
                _lengthWithValue(password);

            // bitconverter does hex like ff-ff-ff so get rid of that
            //System.Windows.MessageBox.Show(BitConverter.ToString(rsa.Encrypt(Encoding.UTF8.GetBytes(payload), false)).Replace("-", ""));
            return BitConverter.ToString(rsa.Encrypt(Encoding.UTF8.GetBytes(payload), false)).Replace("-", "").ToLower();
        }

        // makes it easier above
        private static string _lengthWithValue(string s)
        {
            return Convert.ToChar(s.Length).ToString() + s;
        }

        // https://stackoverflow.com/questions/321370/how-can-i-convert-a-hex-string-to-a-byte-array
        private static byte[] hexToByteArray(string s)
        {
            return Enumerable.Range(0, s.Length)
                     .Where(x => x % 2 == 0)
                     .Select(x => Convert.ToByte(s.Substring(x, 2), 16))
                     .ToArray();
        }

        public class WrongCaptchaException : Exception {}

        private Regex loginSuccessRegex = new Regex("location\\.replace\\(\"(.*?)\"\\);");
        private Regex captchaRegex = new Regex("name=\"chptchakey\" id=\"chptchakey\" value=\"(.*?)\"");
        private async Task postLogin(string encrypted, string captcha = null)
        {
            var _postData = new Dictionary<string, string>
            {
                // re: bvsd - login page `bvsd.f(function(a) { console.log(a) })` -> e=1
                // I'm not too sure what it does, but it lets us avoid captcha... so yeah?
                // However idk how to do this so i jsut included captcha :((
                { "enctp", "1" },
                { "encpw", encrypted },
                { "encnm", LoginKeys.KeyName },
                { "svctype", "0" },
                { "svc", "" },
                { "viewtype", "0" },
                { "locale", "ko_KR" },
                { "postDataKey", "" },
                { "smart_LEVEL", "-1" },
                { "logintp", "" },
                { "url", "http://www.naver.com" },
                { "localechange", "" },
                { "ls", "" },
                { "xid", "" },
                { "pre_id", "" },
                { "resp", "" },
                { "ru", "" },
                { "id", "" },
                { "pw", "" },
                { "nvlong", "on" } // tick "stay logged in" - so we're in for longer
            };
            if (WaitingCaptcha)
            {
                _postData.Add("chptcha", captcha);
                _postData.Add("chptchakey", CaptchaKey);
                _postData.Add("captcha_type", "image");
            }

            var postData = new FormUrlEncodedContent(_postData);

            //HttpResponseMessage res = await WebClient.PostAsync(LOGIN_URL, postData);
            HttpRequestMessage req = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(LOGIN_URL)
            };
            req.Content = postData;
            req.Headers.Add("Host", "nid.naver.com");
            req.Headers.Add("Referer", "https://nid.naver.com/nidlogin.login");
            req.Headers.Add("Accept", "text/html, application/xhtml+xml, image/jxr, */*");
            HttpResponseMessage res = await WebClient.SendAsync(req);
            res.EnsureSuccessStatusCode();

            string data = await res.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine(data);

            // check for otp
            if (data.Contains("<title>2차인증 로그인</title>"))
            {
                WaitingCaptcha = false;
                WaitingOtp = true;
                otpData = data;
                return;
            }

            // check for captcha :(
            if (data.Contains("chptchakey"))
            {
                if (WaitingCaptcha)
                    throw new WrongCaptchaException();

                WaitingCaptcha = true;
                CaptchaKey = captchaRegex.Match(data).Groups[1].Value;
                otpData = data;
                return;
            }
            WaitingCaptcha = false;

            // redirect regex
            Match m = loginSuccessRegex.Match(data);
            if (!m.Success)
                throw new Exception();

            await WebClient.GetAsync(m.Groups[1].Value);
            SaveCookies();
            LoggedIn = true;
        }

        public async Task Login(string username, string password, string captcha = null)
        {
            await getKeys();

            RSACryptoServiceProvider rsa = loadKeysInRsa();
            string loginPayload = encryptIdWithRsa(rsa, username, password);

            await postLogin(loginPayload, captcha);
        }

        public class LoginSessionExpiredException : Exception { }
        
        public async Task GetUserDetails()
        {
            var req = new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(PROFILE_URL)
            };
            req.Headers.Add("Host", "static.nid.naver.com");
            req.Headers.Add("Referer", "https://my.naver.com/");

            HttpResponseMessage res = await WebClient.SendAsync(req);

            string rawData = await res.Content.ReadAsStringAsync();
            if (rawData.Contains("Failure"))
            {
                LoggedIn = false;
                throw new LoginSessionExpiredException();
            }

            res.EnsureSuccessStatusCode();
            dynamic data = JObject.Parse(rawData);

            Nickname = data["nick_name"];
            AvatarUrl = data["image_url"];
            LoggedIn = true;
        }

        private Regex otpKeyRegex = new Regex("id=\"key\" name=\"key\" value=\"(.*?)\"");
        private Regex otpTokenRegex = new Regex("name=\"token_push\" id=\"token_push\" value=\"(.*?)\"");
        public async Task DoOtp()
        {
            if (!WaitingOtp)
                return;

            Match keyM = otpKeyRegex.Match(otpData);
            string key = keyM.Groups[1].Value;
            Match tokenM = otpTokenRegex.Match(otpData);
            string token = tokenM.Groups[1].Value;

            // the form side can handle the waiting - but give 5 mins
            // poll every 2 seconds & increase the count
            int count = 0;
            while (true)
            {
                if (!WaitingOtp)
                    return;

                HttpResponseMessage res = await WebClient.GetAsync(OTP_STATUS_URL + $"?token_push={token}&c={count}");
                res.EnsureSuccessStatusCode();
                string data = await res.Content.ReadAsStringAsync();
                if (data.Contains("success"))
                {
                    WaitingOtp = false;
                    break;
                }

                ++count;
                await Task.Delay(2000);
            }

            // finalize login
            var _postData = new Dictionary<string, string>
            {
                { "auto", "" },
                { "enctp", "2" },
                { "encpw", "" },
                { "encnm", "" },
                { "exp", "" },
                { "key", key },
                { "svctype", "0" },
                { "svc", "" },
                { "viewtype", "0" },
                { "locale", "ko_KR" },
                { "postDataKey", "" },
                { "smart_LEVEL", "-1" },
                { "logintp", "" },
                { "url", "http://www.naver.com" },
                { "localechange", "" },
                { "pre_id", "" },
                { "resp", "" },
                { "ru", "" },
                { "mode", "otp" },
                { "otp", "" },
                { "secret_yn", "Y" },
                { "token_push", token }
            };
            var postData = new FormUrlEncodedContent(_postData);

            HttpResponseMessage res2 = await WebClient.PostAsync(LOGIN_URL + "?svctype=0", postData);
            res2.EnsureSuccessStatusCode();

            string data2 = await res2.Content.ReadAsStringAsync();
            // redirect regex
            Match m = loginSuccessRegex.Match(data2);
            if (!m.Success)
                throw new Exception();

            await WebClient.GetAsync(m.Groups[1].Value);
            SaveCookies();
            LoggedIn = true;
        }

        public class NaverKeySet
        {
            public string SessionKey { get; set; }
            public string KeyName { get; set; }
            public string EValue { get; set; }
            public string NValue { get; set; }
        }
    }
}
