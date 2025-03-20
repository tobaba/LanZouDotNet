# Lanzou API 使用示例

以下是如何使用 `Lanzou` 类解析链接的示例代码。

## 初始化

```csharp
var lanzou = new Lanzou();
```

## 解析不带密码的链接

```csharp
var result1 = await lanzou.ParseUrl("https://www.lanzoup.com/XXXXXX");
```

## 解析带密码的链接

```csharp
// var result1 = await lanzou.ParseUrl("https://www.lanzoup.com/YYYYYYY", "b4zb");
```

## 处理结果

```csharp
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
