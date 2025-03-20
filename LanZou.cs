using System.Text.Json;
using System.Text.RegularExpressions;

namespace LanZouApi;

public class LanZou
{
    private readonly HttpClient _httpClient;
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/72.0.3626.121 Safari/537.36";

    public LanZou()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
    }

    public class LanzouResponse
    {
        public int Code { get; set; }
        public string? Msg { get; set; }
        public string? Name { get; set; }
        public string? FileSize { get; set; }
        public string? DownUrl { get; set; }
    }

    public async Task<LanzouResponse> ParseUrl(string url, string pwd = "")
    {
        try
        {
            // 检查URL是否为空
            if (string.IsNullOrEmpty(url))
            {
                return new LanzouResponse { Code = 400, Msg = "请输入URL" };
            }

            // 处理URL
            url = "https://www.lanzoup.com/" + url.Split(".com/")[1];

            // 获取页面内容
            var softInfo = await MloocCurlGet(url);

            // 检查文件是否失效
            if (softInfo.Contains("文件取消分享了"))
            {
                return new LanzouResponse { Code = 400, Msg = "文件取消分享了" };
            }

            // 获取文件名和大小
            string fileName = "";
            string fileSize = "";
            var nameMatch = Regex.Match(softInfo, @"style=""font-size: 30px;text-align: center;padding: 56px 0px 20px 0px;"">(.*?)</div>");
            if (!nameMatch.Success)
            {
                nameMatch = Regex.Match(softInfo, @"<div class=""n_box_3fn"".*?>(.*?)</div>");
            }
            if (nameMatch.Success)
            {
                fileName = nameMatch.Groups[1].Value;
            }

            var sizeMatch = Regex.Match(softInfo, @"<div class=""n_filesize"".*?>大小：(.*?)</div>");
            if (!sizeMatch.Success)
            {
                sizeMatch = Regex.Match(softInfo, @"<span class=""p7"">文件大小：</span>(.*?)<br>");
            }
            if (sizeMatch.Success)
            {
                fileSize = sizeMatch.Groups[1].Value;
            }

            string? downUrl;
            // 处理带密码的链接
            if (softInfo.Contains("function down_p(){"))
            {
                if (string.IsNullOrEmpty(pwd))
                {
                    return new LanzouResponse { Code = 400, Msg = "请输入分享密码" };
                }

                var segmentMatch = Regex.Match(softInfo, @" v3c = '(.*?)';");
                var ajaxmMatch = Regex.Match(softInfo, @"ajaxm\.php\?file=(\d+)");

                if (!segmentMatch.Success || !ajaxmMatch.Success)
                {
                    return new LanzouResponse { Code = 400, Msg = "解析失败" };
                }

                var postData = new Dictionary<string, string>
                {
                    { "action", "downprocess" },
                    { "sign", segmentMatch.Groups[1].Value },
                    { "p", pwd },
                    { "kd", "1" }
                };

                var response = await MloocCurlPost(postData, $"https://www.lanzoup.com/{ajaxmMatch.Groups[0].Value}", url);
            
                var jsonResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(response);

                if (jsonResponse != null && jsonResponse["zt"].GetInt32() != 1)
                {
                    return new LanzouResponse { Code = 400, Msg = jsonResponse["inf"].GetString() };
                }

                // 拼接下载链接
                var downUrl1 = $"{jsonResponse?["dom"].GetString()}/file/{jsonResponse?["url"].GetString()}";
                var downUrl2 = await MloocCurlHead(downUrl1);

                downUrl = string.IsNullOrEmpty(downUrl2) ? downUrl1 : downUrl2;

                // 移除pid参数
                downUrl = Regex.Replace(downUrl, @"pid=(.*?.)&", "");
            }
            else
            {
                // 处理不带密码的链接
                var linkMatch = Regex.Match(softInfo, @"\n<iframe.*?name=""[\s\S]*?""\ssrc=""/(.*?)""");
                if (!linkMatch.Success)
                {
                    linkMatch = Regex.Match(softInfo, @"<iframe.*?name=""[\s\S]*?""\ssrc=""/(.*?)""");
                }

                if (!linkMatch.Success)
                {
                    return new LanzouResponse { Code = 400, Msg = "解析失败" };
                }

                var ifurl = "https://www.lanzoup.com/" + linkMatch.Groups[1].Value;
                softInfo = await MloocCurlGet(ifurl);

                var segmentMatch = Regex.Match(softInfo, @"wp_sign = '(.*?)'");
                var ajaxmMatch = Regex.Matches(softInfo, @"ajaxm\.php\?file=(\d+)");

                if (!segmentMatch.Success || ajaxmMatch.Count < 2)
                {
                    return new LanzouResponse { Code = 400, Msg = "解析失败" };
                }

                var postData = new Dictionary<string, string>
                {
                    { "action", "downprocess" },
                    { "signs", "?ctdf" },
                    { "sign", segmentMatch.Groups[1].Value },
                    { "kd", "1" },{ "ves", "1" }
                };

                var response = await MloocCurlPost(postData, $"https://www.lanzoup.com/{ajaxmMatch[1].Value}", ifurl);
                var jsonResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(response);

                if (jsonResponse != null && jsonResponse["zt"].GetInt32() != 1)
                {
                    return new LanzouResponse { Code = 400, Msg = jsonResponse["inf"].GetString() };
                }

                // 拼接下载链接
                var downUrl1 = $"{jsonResponse?["dom"].GetString()}/file/{jsonResponse?["url"].GetString()}";
                var downUrl2 = await MloocCurlHead(downUrl1);

                downUrl = string.IsNullOrEmpty(downUrl2) ? downUrl1 : downUrl2;
                

                // 移除pid参数
                downUrl = Regex.Replace(downUrl, @"pid=(.*?.)&", "");
            }

            // 返回结果
            return new LanzouResponse
            {
                Code = 200,
                Msg = "解析成功",
                Name = fileName,
                FileSize = fileSize,
                DownUrl = downUrl
            };
        }
        catch (Exception ex)
        {
            return new LanzouResponse { Code = 400, Msg = $"解析失败: {ex.Message}" };
        }
    }

    private async Task<string> MloocCurlGet(string url)
    {
        _httpClient.DefaultRequestHeaders.Add("X-FORWARDED-FOR", RandIp());
        _httpClient.DefaultRequestHeaders.Add("CLIENT-IP", RandIp());
        var response = await _httpClient.GetStringAsync(url);
        return response;
    }

    private async Task<string> MloocCurlPost(Dictionary<string, string> postData, string url, string referer = "")
    {
        var content = new FormUrlEncodedContent(postData);
        if (!string.IsNullOrEmpty(referer))
        {
            _httpClient.DefaultRequestHeaders.Referrer = new Uri(referer);
        }

        var ip = RandIp();
        _httpClient.DefaultRequestHeaders.Add("X-FORWARDED-FOR", ip);
        _httpClient.DefaultRequestHeaders.Add("CLIENT-IP", ip);
        var response = await _httpClient.PostAsync(url, content);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string?> MloocCurlHead(string? url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
        request.Headers.Add("Accept-Encoding", "gzip, deflate");
        request.Headers.Add("Accept-Language", "zh-CN,zh;q=0.9");
        request.Headers.Add("Cache-Control", "no-cache");
        request.Headers.Add("Connection", "keep-alive");
        request.Headers.Add("Pragma", "no-cache");
        request.Headers.Add("Upgrade-Insecure-Requests", "1");
        request.Headers.Add("Referer", "https://developer.lanzoug.com");
        request.Headers.Add("Cookie", "down_ip=1; expires=Sat, 16-Nov-2019 11:42:54 GMT; path=/; domain=.baidupan.com");

        var response = await _httpClient.SendAsync(request);
        if (response.RequestMessage?.RequestUri != null)
        {
            return response.RequestMessage.RequestUri.ToString();
        }
        return string.Empty;
    }

    private  string RandIp()
    {
        var random = new Random();
        var ip2Id = random.Next(60, 255);
        var ip3Id = random.Next(60, 255);
        var ip4Id = random.Next(60, 255);

        var arr1 = new[] { "218", "218", "66", "66", "218", "218", "60", "60", "202", "204", "66", "66", "66", "59", "61", "60", "222", "221", "66", "59", "60", "60", "66", "218", "218", "62", "63", "64", "66", "66", "122", "211" };
        var ip1Id = arr1[random.Next(arr1.Length)];

        return $"{ip1Id}.{ip2Id}.{ip3Id}.{ip4Id}";
    }
}