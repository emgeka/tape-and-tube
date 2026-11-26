// SPDX-License-Identifier: GPL-3.0-only
using System.Text.Json.Serialization;

namespace Ntscrt.Windows;

[JsonConverter(typeof(JsonStringEnumConverter<HistoricalDisplay>))]
internal enum HistoricalDisplay { None, Baird30, British405, Kinescope }
