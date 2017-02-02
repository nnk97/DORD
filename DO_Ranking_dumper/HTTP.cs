using System.Net;
using System.IO;

namespace DO_Ranking_dumper
{
    public class HTTPMgr
    {
        private CookieContainer m_Cookies;

        public HTTPMgr()
        {
            m_Cookies = new CookieContainer();
        }

        public bool GET(string _Url, out string _HTML)
        {
            var Req = (HttpWebRequest)(WebRequest.Create(_Url));
            Req.Method = "GET";
            Req.CookieContainer = m_Cookies;
            using (var Resp = (HttpWebResponse)Req.GetResponse())
            {
                if (Resp.StatusCode != HttpStatusCode.OK)
                {
                    _HTML = string.Empty;
                    return false;
                }

                m_Cookies.Add(Resp.Cookies);
                using (var Reader = new StreamReader(Resp.GetResponseStream()))
                    _HTML = Reader.ReadToEnd();

                return true;
            }
        }
    }
}
