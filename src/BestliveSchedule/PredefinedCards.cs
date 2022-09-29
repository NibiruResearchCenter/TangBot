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

using BestliveSchedule.Model;
using DodoHosted.Base.Card;
using DodoHosted.Base.Card.BaseComponent;
using DodoHosted.Base.Card.CardComponent;
using DodoHosted.Base.Card.Enums;

namespace BestliveSchedule;

public static class PredefinedCards
{
    public static CardMessage GetOhayoCard(this LiveEvent[] events, DateTimeOffset time)
    {
        var card = new CardMessage(new Card
        {
            Title = "早安，尼比陆！",
            Theme = CardTheme.Purple,
            Components = new List<ICardComponent>
            {
                new TextFiled($"现在是 {time.Year:0000} 年 {time.Month:00} 月 {time.Day:00} 日，上午 {time.Hour:00} 时 {time.Minute:00} 分"),
                new Divider(),
                new TextFiled("今日直播时间表~")
            }
        });

        if (events.Length == 0)
        {
            card.AddComponent(new TextFiled("**暂无**"));
        }
        else
        {
            foreach (var e in events)
            {
                card.AddComponent(new MultilineText(new Text(e.Time), new Text(e.User), new Text(e.Title)));
            }
        }

        return card;
    }
}
