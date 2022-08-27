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

using System.Text.Json;
using BilibiliLiveInformer.Entity;

namespace BilibiliLiveInformer;

public static class BiliApi
{
    public static string ApiRequestRate
    {
        get
        {
            if (s_apiRequestCount == 0)
            {
                return "NaN reqs/min";
            }
            var duration = (DateTimeOffset.UtcNow - s_startTime).TotalSeconds;
            var minute = duration / 60;
            var hour = minute / 60;
            var rate = s_apiRequestCount / minute;

            return $"{rate:0.00} reqs/min ({s_apiRequestCount} reqs in {minute:0.00} minutes or {hour:0.00} hours)";
        }
    }

    public static string FailedRequestRate
    {
        get
        {
            if (s_failedRequestCount == 0)
            {
                return "NaN reqs/min";
            }
            var duration = (DateTimeOffset.UtcNow - s_startTime).TotalSeconds;
            var minute = duration / 60;
            var hour = minute / 60;
            var rate = s_failedRequestCount / minute;

            return $"{rate:0.0000} reqs/min ({s_failedRequestCount} reqs in {minute:0.00} minutes or {hour:0.00} hours)";
        }
    }

    private static DateTimeOffset s_startTime = DateTimeOffset.UtcNow;
    private static int s_apiRequestCount;
    private static int s_failedRequestCount;
    
    private static readonly HttpClient s_httpClient = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.5112.102 Safari/537.36 Edg/104.0.1293.63" }
        }
    };
    
    public static async Task<BiliApiListener.ExecutionResult> GetLiveStatus(string uid, CancellationToken token)
    {
        var content = await GetBiliUserInfo(uid, token);
        
        var json = await JsonDocument.ParseAsync(content, cancellationToken: token);
        var liveRoom = json.RootElement.GetProperty("data").GetProperty("live_room");
                
        var isLive = liveRoom.GetProperty("liveStatus").GetInt32() is 1;
        var title = liveRoom.GetProperty("title").GetString()!;

        return new BiliApiListener.ExecutionResult(isLive, title);
    }

    public static async Task<(SubscribedLiver?, Exception?)> GetLiverInfo(string uid)
    {
        try
        {
            var content = await GetBiliUserInfo(uid, CancellationToken.None);
        
            var json = await JsonDocument.ParseAsync(content);
            var data = json.RootElement.GetProperty("data");

            var name = data.GetProperty("name").GetString()!;
            var roomId = data.GetProperty("live_room").GetProperty("roomid").GetInt64().ToString();

            return (
                new SubscribedLiver
                {
                    BiliUid = uid,
                    BiliUname = name,
                    BiliLiveRoomId = roomId,
                    NotifyChannels = new List<NotifyChannel>()
                }, null);
        }
        catch (Exception exception)
        {
            return (null, exception);
        }
    }

    private static async Task<Stream> GetBiliUserInfo(string uid, CancellationToken token)
    {
        try
        {
            var content = await s_httpClient.GetAsync($"https://api.bilibili.com/x/space/acc/info?mid={uid}&jsonp=jsonp", token);
            
            content.EnsureSuccessStatusCode();
            
            s_apiRequestCount++;
            CheckStatisticReset();
            
            return await content.Content.ReadAsStreamAsync(token);
        }
        catch (Exception)
        {
            s_failedRequestCount++;
            CheckStatisticReset();
            
            throw;
        }
    }

    private static void CheckStatisticReset()
    {
        if (s_apiRequestCount != int.MaxValue && s_failedRequestCount != int.MaxValue)
        {
            return;
        }

        s_apiRequestCount = 0;
        s_failedRequestCount = 0;
        s_startTime = DateTimeOffset.UtcNow;
    }
}
