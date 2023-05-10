using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using StackExchange.Redis;
using System.Text.Json;

const int TIMEOUT_MS = 60_000;

Conf _conf = Deserialize<Conf>(GetEnvValue("CONF"));
HttpClient _scClient = new();

#region redis

ConnectionMultiplexer redis = ConnectionMultiplexer.Connect($"{_conf.RdsServer},password={_conf.RdsPwd},name=Note163Checkin,defaultDatabase=0,allowadmin=true,abortConnect=false");
IDatabase db = redis.GetDatabase();
bool isRedis = db.IsConnected("test");
Console.WriteLine("redis:{0}", isRedis ? "有效" : "无效");

var httpClient = new HttpClient();
var formData = new MultipartFormDataContent();
formData.Add(new StringContent("xukuan"), "username");
formData.Add(new StringContent("MTIzNDU2"), "passc");
formData.Add(new StringContent("MTAwMDIxNjM2Mw=="), "USERID");
var response = await httpClient.PostAsync("https://www.rfidfans.com/upload/qiandao.php", formData);
string resultStr = response.Content.ReadAsStringAsync().Result;
Console.WriteLine(resultStr);
    

   
       
//Console.WriteLine("签到运行完毕");
await Notify("签到结果("+resultNotify+")", isNotify);

async Task<(bool isInvalid, string result)> IsInvalid(string cookie)
{
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("User-Agent", "ynote-android");
    client.DefaultRequestHeaders.Add("Cookie", cookie);
    //每日打开客户端（即登陆）
    string result = await (await client.PostAsync("https://note.youdao.com/yws/api/daupromotion?method=sync", null))
        .Content.ReadAsStringAsync();
    //await Notify($"cookie内容： {cookie }");
    //await Notify($"验证结果： {result }");
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

async Task Notify(string msg, bool isFailed = false)
{
    Console.WriteLine(msg);
    if (_conf.ScType == "Always" || (isFailed && _conf.ScType == "Failed"))
    {
        await _scClient.GetAsync($"https://sc.ftqq.com/{_conf.ScKey}.send?text={msg}");
    }
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
