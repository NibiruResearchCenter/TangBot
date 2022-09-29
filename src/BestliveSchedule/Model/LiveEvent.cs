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

using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace BestliveSchedule.Model;

public record LiveEvent(string Time, string User, string Title)
{
    public string Time { get; set; } = Time;
    public string User { get; set; } = User;
    public string Title { get; set; } = Title;
}
