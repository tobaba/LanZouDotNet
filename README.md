var lanzou = new LanZou();

// 解析不带密码的链接
var result1 = await lanzou.ParseUrl("https://www.lanzoup.com/XXXXXX");

//// 解析带密码的链接
//var result1 = await lanzou.ParseUrl("https://www.lanzoup.com/YYYYYYY", "b4zb");


if (result1.Code == 200)
{
    Console.WriteLine($"文件名：{result1.Name}");
    Console.WriteLine($"文件大小：{result1.FileSize}");
    Console.WriteLine($"下载地址：{result1.DownUrl}");
}
else
{
    Console.WriteLine($"错误：{result1.Msg}");
}
