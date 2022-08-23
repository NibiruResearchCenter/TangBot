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

using DodoHosted.Base.Card;
using DodoHosted.Base.Card.BaseComponent;
using DodoHosted.Base.Card.CardComponent;
using DodoHosted.Base.Card.Enums;

namespace LiveSchedule;

public static class CardPredefined
{
    public static CardMessage AddNewLiveFormCard(DateOnly start, DateOnly end) => new()
    {
        Content = "直播周表填写",
        Card = new Card
        {
            Title = "直播周表填写",
            Theme = CardTheme.Purple,
            Components = new List<ICardComponent>
            {
                new Header
                {
                    Text = new Text
                    {
                        Type = ContentTextType.DodoMarkdown,
                        Content = $"**{start.ToString("yyyy-MM-dd")}** 至 **{end.ToString("yyyy-MM-dd")}** 直播时间表"
                    }
                },
                new Header
                {
                    Text = new Text
                    {
                        Type = ContentTextType.DodoMarkdown,
                        Content = $"请在 `{start.AddDays(-1).ToString("yyyy-MM-dd")} 23:59` 前填写"
                    }
                },
                new TextFiled
                {
                    Text = new Text
                    {
                        Type = ContentTextType.DodoMarkdown,
                        Content = "请注意，多次使用 \"添加周表\" 将只取最后一次提交的内容"
                    }
                },
                new ButtonGroup
                {
                    Buttons = new List<Button>
                    {
                        new()
                        {
                            InteractCustomId = $"{CardConstant.CARD_BUTTON_ADD_NEW_WEEKLY_LIVE_SCHEDULE}-" +
                                               $"{start.ToString("yyyy-MM-dd")}-{end.ToString("yyyy-MM-dd")}",
                            Click = new ButtonAction(ButtonActionType.Form, string.Empty),
                            Color = ButtonColor.Blue,
                            Name = "添加周表",
                            Form = WeeklyLiveScheduleForm()
                        },
                        new()
                        {
                            InteractCustomId = $"{CardConstant.CARD_BUTTON_DELETE_WEEKLY_LIVE_SCHEDULE}-" +
                                               $"{start.ToString("yyyy-MM-dd")}-{end.ToString("yyyy-MM-dd")}",
                            Click = new ButtonAction(ButtonActionType.Form, string.Empty),
                            Color = ButtonColor.Red,
                            Name = "删除已添加的周表",
                            Form = DeleteWeeklyLiveScheduleForm()
                        }
                    }
                }
            }
        }
    };

    private static Form WeeklyLiveScheduleForm() => new()
    {
        Title = "新的直播周表",
        Elements = new List<Input>
        {
            new()
            {
                Key = CardConstant.CARD_FORM_LIVER_DODO_ID,
                MinChar = 0,
                MaxChar = 1000,
                Rows = 1,
                Title = "Liver 渡渡语音 ID",
                Placeholder = "仅供管理员使用，Liver 不必填写此项"
            },
            new()
            {
                Key = CardConstant.CARD_FORM_WEEKLY_LIVE_SCHEDULE,
                MinChar = 1,
                MaxChar = 4000,
                Rows = 4,
                Title = "周表，格式请参考文档说明"
            }
        }
    };

    private static Form DeleteWeeklyLiveScheduleForm() => new()
    {
        Title = "移除已添加的直播周表",
        Elements = new List<Input>
        {
            new()
            {
                Key = CardConstant.CARD_FORM_LIVER_DODO_ID,
                MinChar = 0,
                MaxChar = 1000,
                Rows = 1,
                Title = "Liver 渡渡语音 ID",
                Placeholder = "仅供管理员使用，Liver 不必填写此项"
            }
        }
    };
}
