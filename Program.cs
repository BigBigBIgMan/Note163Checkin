using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using StackExchange.Redis;
using System.Text.Json;
using System.Text;
using System.Net;
using System.Net.Http.Headers;

const int TIMEOUT_MS = 60_000;

Conf _conf = Deserialize<Conf>(GetEnvValue("CONF"));
HttpClient _scClient = new();

#region redis

ConnectionMultiplexer redis = ConnectionMultiplexer.Connect($"{_conf.RdsServer},password={_conf.RdsPwd},name=Note163Checkin,defaultDatabase=0,allowadmin=true,abortConnect=false");
IDatabase db = redis.GetDatabase();
bool isRedis = db.IsConnected("test");
Console.WriteLine("redis:{0}", isRedis ? "有效" : "无效");

//var httpClient = new HttpClient();
//var formData = new MultipartFormDataContent();

//formData.Add(new StringContent("xukuan", Encoding.UTF8, "text/plain"), "username");
//formData.Add(new StringContent("MTIzNDU2", Encoding.UTF8, "text/plain"), "passc");
//formData.Add(new StringContent("MTAwMDIxNjM2Mw==", Encoding.UTF8, "text/plain"), "USERID");

//var txtusername = new ByteArrayContent(Encoding.UTF8.GetBytes("xukuan"));
//txtusername.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
//                    {
//                        Name = "username"
//                    };
//                    
//var txtpassc = new ByteArrayContent(Encoding.UTF8.GetBytes("MTIzNDU2"));
//formData.Add(txtusername);
//txtpassc.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
//                    {
//                        Name = "passc"
//                    };
//formData.Add(txtpassc);
//var txtUSERID = new ByteArrayContent(Encoding.UTF8.GetBytes("MTAwMDIxNjM2Mw=="));
//txtUSERID.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
//                    {
//                        Name = "USERID"
//                    };
//formData.Add(txtUSERID);

//var response = await httpClient.PostAsync("https://www.rfidfans.com/upload/qiandao.php", formData);
//byte[] buf = await response.Content.ReadAsByteArrayAsync();
//string resultStr = Encoding.UTF8.GetString(buf);
//string resultStr = response.Content.ReadAsStringAsync().Result;
//Console.WriteLine(resultStr);


//string serviceAddress = "https://www.rfidfans.com/upload/qiandao.php";
//HttpWebRequest request = (HttpWebRequest)WebRequest.Create(serviceAddress);
 //
 //request.Method = "POST";
 //request.ContentType = "application/json";
 //string strContent = "{ \"username\": \"xukuan\",\"passc\": \"MTIzNDU2\",\"USERID\": \"MTAwMDIxNjM2Mw==\"}";
 ////string strContent = data; //参数data的格式就是上一句被注释的语句
 //using (StreamWriter dataStream = new StreamWriter(request.GetRequestStream()))
 //{
 //    dataStream.Write(strContent);
 //    dataStream.Close();
 //}
 //HttpWebResponse response = (HttpWebResponse)request.GetResponse();
 //string encoding = response.ContentEncoding;
 //if (encoding == null || encoding.Length < 1)
 //{
 //    encoding = "UTF-8"; //默认编码  
 //}
 //StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding(encoding));
 //string retString = reader.ReadToEnd();
 //Console.WriteLine(retString);
//try
//{
//            Encoding encoding = Encoding.UTF8;
//            Stream outstream = null;
//            Stream instream = null;
//            StreamReader sr = null;
//            string url = "https://www.rfidfans.com/upload/qiandao.php";
//            HttpWebRequest request = null;
//            HttpWebResponse response = null;
//            
//            // 准备请求,设置参数
//            request = WebRequest.Create(url) as HttpWebRequest;
//            request.Method = "POST";
//            request.ContentType ="application/x-www-form-urlencoded";           
//            byte[] data = encoding.GetBytes("username=xukuan&passc=MTIzNDU2&USERID=MTAwMDIxNjM2Mw==");
//            request.ContentLength = data.Length;
//            outstream = request.GetRequestStream();
//            outstream.Write(data, 0, data.Length);
//            outstream.Flush();
//            outstream.Close();
//            //发送请求并获取相应回应数据
//            response = request.GetResponse() as HttpWebResponse;
//            //直到request.GetResponse()程序才开始向目标网页发送Post请求
//            instream = response.GetResponseStream();
//            sr = new StreamReader(instream, encoding);
//            //返回结果网页(html)代码
//            string content = sr.ReadToEnd();
//            Console.WriteLine(content);
//}
//catch(Exception)
//{
//            Console.WriteLine("PCR532签到失败");
//}

   #endregion
string text = "";
Console.WriteLine("有道云笔记签到开始运行...");
text = "有道云笔记签到开始运行...";

string resultNotify = "";
for (int i = 0; i < _conf.Users.Length; i++)
{
    User user = _conf.Users[i];
    string title = $"账号 {i + 1}: {user.Task} ";
    Console.WriteLine($"共 {_conf.Users.Length} 个账号，正在运行{title}...");
    text = text + "\n\n" + $"共 {_conf.Users.Length} 个账号，正在运行{title}...";
    #region 获取cookie

    string cookie = string.Empty;
    bool isInvalid = true; string result = string.Empty;

    string redisKey = $"Note163_{user.Username}";
    if (isRedis)
    {
        var redisValue = await db.StringGetAsync(redisKey);
        if (redisValue.HasValue)
        {
            cookie = redisValue.ToString();
            (isInvalid, result) = await IsInvalid(cookie);
            Console.WriteLine("redis获取cookie,状态:{0}", isInvalid ? "无效" : "有效");
            text =  text + "\n\n" + ("redis获取cookie,状态:" + (isInvalid ? "无效" : "有效"));
        }
    }

    if (isInvalid)
    {
        cookie = await GetCookie(user);
        (isInvalid, result) = await IsInvalid(cookie);
        Console.WriteLine("login获取cookie,状态:{0}", isInvalid ? "无效" : "有效");
        text =  text + "\n\n" + ("login获取cookie,状态:"+(isInvalid ? "无效" : "有效"));
        if (isInvalid)
        {//Cookie失效
            Console.WriteLine($"{title}Cookie失效，请检查登录状态！"); 
            text =  text + "\n\n" + $"{title}Cookie失效，请检查登录状态！";
            continue;
        }
    }

    if (isRedis)
    {
        Console.WriteLine($"redis更新cookie:{await db.StringSetAsync(redisKey, cookie)}");
        text =  text + "\n\n" + ($"redis更新cookie:{await db.StringSetAsync(redisKey, cookie)}");
    }

    #endregion

    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("User-Agent", "ynote-android");
    client.DefaultRequestHeaders.Add("Cookie", cookie);

    long space = 0;
    space += Deserialize<YdNoteRsp>(result).RewardSpace;
    //签到
    result = await (await client.PostAsync("https://note.youdao.com/yws/mapi/user?method=checkin", null))
       .Content.ReadAsStringAsync();
    space += Deserialize<YdNoteRsp>(result).Space;

    //看广告
    for (int j = 0; j < 3; j++)
    {
        result = await (await client.PostAsync("https://note.youdao.com/yws/mapi/user?method=adPrompt", null))
           .Content.ReadAsStringAsync();
        space += Deserialize<YdNoteRsp>(result).Space;
    }

    //看视频广告
    for (int j = 0; j < 3; j++)
    {
        result = await (await client.PostAsync("https://note.youdao.com/yws/mapi/user?method=adRandomPrompt", null))
           .Content.ReadAsStringAsync();
        space += Deserialize<YdNoteRsp>(result).Space;
    }
    resultNotify += i+"："+(space / 1048576)+"M;";
    Console.WriteLine($"有道云笔记{title}签到成功，共获得空间 {space / 1048576} M");   
    text =  text + ($"有道云笔记{title}签到成功，共获得空间 {space / 1048576} M");  
   
}

   
       
//Console.WriteLine("签到运行完毕");
await Notify("签到结果("+resultNotify+")", text );

async Task<(bool isInvalid, string result)> IsInvalid(string cookie)
{
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("User-Agent", "ynote-android");
    client.DefaultRequestHeaders.Add("Cookie", cookie);
    //每日打开客户端（即登陆）
    string result = await (await client.PostAsync("https://note.youdao.com/yws/api/daupromotion?method=sync", null))
        .Content.ReadAsStringAsync();
    return (result.Contains("error", StringComparison.OrdinalIgnoreCase), result);
}

async Task<string> GetCookie(User user)
{
    var launchOptions = new LaunchOptions
    {
        Headless = false,
        DefaultViewport = null,
        ExecutablePath = @"/usr/bin/google-chrome"
    };
    var browser = await Puppeteer.LaunchAsync(launchOptions);
    IPage page = await browser.DefaultContext.NewPageAsync();

    await page.GoToAsync("https://note.youdao.com/web", TIMEOUT_MS);

    bool isLogin = false;
    string cookie = "fail";
    try
    {
        #region 登录

        //登录
        _ = Login(page, user);
        int totalDelayMs = 0, delayMs = 100;
        while (true)
        {
            if ((isLogin = IsLogin(page))
                || totalDelayMs > TIMEOUT_MS)
            {
                break;
            }
            await Task.Delay(delayMs);
            totalDelayMs += delayMs;
        }

        if (isLogin)
        {
            var client = await page.Target.CreateCDPSessionAsync();
            var ckObj = await client.SendAsync("Network.getAllCookies");
            var cks = ckObj.Value<JArray>("cookies")
                .Where(p => p.Value<string>("domain").Contains("note.youdao.com"))
                .Select(p => $"{p.Value<string>("name")}={p.Value<string>("value")}");
            cookie = string.Join(';', cks);
        }

        #endregion
    }
    catch (Exception ex)
    {
        cookie = "ex";
        Console.WriteLine($"处理Page时出现异常！{ex.Message}；{ex.StackTrace}");
    }
    finally
    {
        await browser.DisposeAsync();
    }

    return cookie;
}

async Task Login(IPage page, User user)
{
    try 
    {
        string js = await _scClient.GetStringAsync(_conf.JsUrl);
        await page.EvaluateExpressionAsync(js.Replace("@U", user.Username).Replace("@P", user.Password));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Login时出现异常！{ex.Message}. {ex.StackTrace}");
    }
}

bool IsLogin(IPage page) => !page.Url.Contains(_conf.LoginStr, StringComparison.OrdinalIgnoreCase);

async Task Notify(string msg, string text)
{
    Console.WriteLine(msg);
    text = text+"\n\n \n\n "+msg;
    await _scClient.GetAsync($"http://www.pushplus.plus/send?token={_conf.PpToken}&title={msg}&content={text}");
    await _scClient.GetAsync($"https://sc.ftqq.com/{_conf.ScKey}.send?title={msg}&desp={text}");
}

T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip
});

string GetEnvValue(string key) => Environment.GetEnvironmentVariable(key);

#region Conf

class Conf
{
    public User[] Users { get; set; }
    public string ScKey { get; set; }
    public string PpToken { get; set; }
    public string ScType { get; set; }
    public string RdsServer { get; set; }
    public string RdsPwd { get; set; }
    public string JsUrl { get; set; } = "https://github.com/BlueHtml/pub/raw/main/code/js/note163login.js";
    public string LoginStr { get; set; } = "signIn";
}

class User
{
    public string Task { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
}

#endregion

class YdNoteRsp
{
    /// <summary>
    /// Sync奖励空间
    /// </summary>
    public int RewardSpace { get; set; }

    /// <summary>
    /// 其他奖励空间
    /// </summary>
    public int Space { get; set; }
}
