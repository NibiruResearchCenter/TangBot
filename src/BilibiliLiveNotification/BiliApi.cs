// This file is a part of TangBot project.
// 
// Copyright (C) 2022 NibiruResearchCenter and all Contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BilibiliLiveNotification.Model;

namespace BilibiliLiveNotification;

public static class BiliApi
{
    public static string ApiRequestRate
    {
        get
        {
            if (ApiRequestCount == 0)
            {
                return "NaN reqs/min";
            }
            var duration = (DateTimeOffset.UtcNow - s_startTime).TotalSeconds;
            var minute = duration / 60;
            var rate = ApiRequestCount / minute;

            return $"{rate:0.00} reqs/min";
        }
    }
    public static int ApiRequestCount { get; private set; }

    public static string FailedRequestRate
    {
        get
        {
            if (FailedRequestCount == 0)
            {
                return "NaN reqs/min";
            }
            var duration = (DateTimeOffset.UtcNow - s_startTime).TotalSeconds;
            var minute = duration / 60;
            var hour = minute / 60;
            var rate = FailedRequestCount / minute;

            return $"{rate:0.0000} reqs/min ({FailedRequestCount} reqs in {minute:0.00} minutes or {hour:0.00} hours)";
        }
    }
    public static int FailedRequestCount { get; private set; }

    public static string CountingTimespanMinutes
    {
        get
        {
            var duration = (DateTimeOffset.UtcNow - s_startTime).TotalSeconds;
            var minute = duration / 60;
            return $"{minute:0.00}";
        }
    }
    public static string CountingTimespanHours
    {
        get
        {
            var duration = (DateTimeOffset.UtcNow - s_startTime).TotalSeconds;
            var minute = duration / 60;
            var hour = minute / 60;
            return $"{hour:0.00}";
        }
    }
    
    private static DateTimeOffset s_startTime = DateTimeOffset.UtcNow;

    private static readonly HttpClient s_httpClient = new(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All
    });

    private static readonly List<string> s_userAgents = new()
    {
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.9; rv:24.0) Gecko/20100101 Firefox/24.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/105.0.0.0 Safari/537.36 Edg/105.0.1343.50",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/105.0.0.0 Safari/537.36 Edg/105.0.1343.50",
        "Mozilla/5.0 (X11; Linux i686) AppleWebKit/537.36 (KHTML, like Gecko) Ubuntu Chromium/65.0.3325.181 Chrome/65.0.3325.181 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.5112.81 Safari/537.36 Edg/104.0.1293.47",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/103.0.5060.53 Safari/537.36 Edg/103.0.1264.37",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.5112.102 Safari/537.36 Edg/104.0.1293.70",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/74.0.3729.169 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/56.0.2924.87 Safari/537.36 OPR/43.0.2442.991",
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/534.24 (KHTML, like Gecko) Chrome/89.0.4389.116 Safari/534.24 XiaoMi/MiuiBrowser/13.2.1-gn",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.0.0 Safari/537.36 Herring/100.1.8040.41",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/102.0.0.0 Safari/537.36 Herring/91.1.8770.71"
    };
    
    private static string GetRandomUserAgent()
    {
        var random = new Random();
        var index = random.Next(0, s_userAgents.Count);
        return s_userAgents[index];
    }

    /// <summary>
    /// 获取主播信息
    /// </summary>
    /// <param name="uid">B站用户 UID</param>
    /// <param name="token"></param>
    /// <returns></returns>
    public static async Task<SubscribedLiver> GetLiverInfo(string uid, CancellationToken token)
    {
        var json = await GetBiliUserInfo(uid, token);
        
        var data = json.GetProperty("data");

        var name = data.GetProperty("info").GetProperty("uname").GetString()!;
        var roomId = data.GetProperty("room_id").GetInt64().ToString();

        return new SubscribedLiver
            {
                BiliUid = uid,
                BiliUname = name,
                BiliLiveRoomId = roomId,
                NotifyChannels = new List<NotifyChannel>(),
                CurrentStatus = new CurrentStatus
                {
                    IsLive = false,
                    Title = string.Empty,
                    Cover = string.Empty,
                    CoverFromDodo = string.Empty,
                    MessageIds = new List<string>(),
                    StartTime = DateTimeOffset.UtcNow.AddHours(8)
                }
            };
    }

    /// <summary>
    /// 获取直播间信息
    /// </summary>
    /// <param name="livers"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="HttpRequestException"></exception>
    public static async Task<IEnumerable<LiveCheckResult>> GetLiveStatus(IEnumerable<SubscribedLiver> livers, CancellationToken token)
    {
        try
        {
            var roomIds = livers.Select(x => x.BiliUid).ToArray();
            var payload = new GetLiveStatusPayload(roomIds);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.live.bilibili.com/room/v1/Room/get_status_info_by_uids");
            request.Headers.Add("User-Agent", GetRandomUserAgent());
            request.Headers.Add("Origin", "https://live.bilibili.com");
            request.Headers.Add("Referer", "https://live.bilibili.com/");
            request.Headers.Add("Accept", "application/json, text/plain, */*");
            request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
            request.Headers.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
            request.Headers.Add("Cache-Control", "no-cache");
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var response = await s_httpClient.SendAsync(request, token);
            
            response.EnsureSuccessStatusCode();
            
            ApiRequestCount++;
            CheckStatisticReset();
            
            var str = await response.Content.ReadAsStringAsync(token);
            var json = JsonDocument.Parse(str);
            
            var code = json.RootElement.GetProperty("code").GetInt32();
            var message = json.RootElement.GetProperty("message").GetString();

            if (code != 0)
            {
                throw new HttpRequestException($"请求 Bilibili API 失败，Code：{code}，Message：{message}");
            }

            var data = json.RootElement.GetProperty("data");
            if (data.ValueKind != JsonValueKind.Object)
            {
                throw new HttpRequestException("请求 Bilibili API 失败，data 为 Null");
            }

            var objs = data.EnumerateObject();
            var results = new List<LiveCheckResult>();

            // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
            foreach (var obj in objs)
            {
                var uid = obj.Value.GetProperty("uid").GetInt64().ToString();
                var isLive = obj.Value.GetProperty("live_status").GetInt32() == 1;
                var title = obj.Value.GetProperty("title").GetString()!;
                var cover = obj.Value.GetProperty("cover_from_user").GetString()!;
                var startTimestamp = obj.Value.GetProperty("live_time").GetInt64();

                var startTime = DateTimeOffset.FromUnixTimeSeconds(startTimestamp).AddHours(8);
                
                results.Add(new LiveCheckResult(uid, isLive, title, cover, startTime));
            }

            return results;
        }
        catch (Exception)
        {
            FailedRequestCount++;
            CheckStatisticReset();
            
            throw;
        }
    }
    
    /// <summary>
    /// 获取 B 站用户信息
    /// </summary>
    /// <param name="uid">B站用户 UID</param>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="HttpRequestException"></exception>
    private static async Task<JsonElement> GetBiliUserInfo(string uid, CancellationToken token)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.live.bilibili.com/live_user/v1/Master/info?uid={uid}");
            request.Headers.Add("User-Agent", GetRandomUserAgent());
            request.Headers.Add("Origin", "https://live.bilibili.com");
            request.Headers.Add("Referer", "https://live.bilibili.com/");
            request.Headers.Add("Accept", "application/json, text/plain, */*");
            request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
            request.Headers.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
            request.Headers.Add("Cache-Control", "no-cache");
            var content = await s_httpClient.SendAsync(request, token);
            
            content.EnsureSuccessStatusCode();
            
            ApiRequestCount++;
            CheckStatisticReset();

            var str = await content.Content.ReadAsStringAsync(token);
            var json = JsonDocument.Parse(str);
            
            var code = json.RootElement.GetProperty("code").GetInt32();
            var message = json.RootElement.GetProperty("message").GetString();

            if (code != 0)
            {
                throw new HttpRequestException($"请求 Bilibili API 失败，Code：{code}，Message：{message}");
            }

            var data = json.RootElement.GetProperty("data");
            if (data.ValueKind != JsonValueKind.Object)
            {
                throw new HttpRequestException("请求 Bilibili API 失败，data 为 Null");
            }
            
            return json.RootElement;
        }
        catch (Exception)
        {
            FailedRequestCount++;
            CheckStatisticReset();
            
            throw;
        }
    }
    
    private static void CheckStatisticReset()
    {
        if (ApiRequestCount != int.MaxValue && FailedRequestCount != int.MaxValue)
        {
            return;
        }

        ApiRequestCount = 0;
        FailedRequestCount = 0;
        s_startTime = DateTimeOffset.UtcNow;
    }

    public record LiveCheckResult(string BiliUid, bool IsLive, string Title, string Cover, DateTimeOffset StartTime);

    // ReSharper disable once NotAccessedPositionalProperty.Local
    private record GetLiveStatusPayload([property: JsonPropertyName("uids")] string[] RoomIds);
}
