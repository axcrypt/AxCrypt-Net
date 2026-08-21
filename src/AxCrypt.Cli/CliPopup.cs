// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) AxCrypt AB
//
// This file is part of AxCrypt.
//
// AxCrypt is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// AxCrypt is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with AxCrypt. If not, see <https://www.gnu.org/licenses/>.

using AxCrypt.Common;
using AxCrypt.Core.UI;

namespace AxCrypt.Cli
{
    internal sealed class CliPopup : IPopup
    {
        public Task<PopupButtons> ShowAsync(PopupButtons buttons, string title, string message)
        {
            Write(title, message);
            return Task.FromResult(PopupButtons.Ok);
        }

        public Task<PopupButtons> ShowAsync(PopupButtons buttons, string title, string message, DoNotShowAgainOptions doNotShowAgainOption)
        {
            Write(title, message);
            return Task.FromResult(PopupButtons.Ok);
        }

        public Task<PopupButtons> ShowAsync(PopupButtons buttons, string title, string message, DoNotShowAgainOptions doNotShowAgainOption, string doNotShowAgainCustomText)
        {
            Write(title, message);
            return Task.FromResult(PopupButtons.Ok);
        }

        public Task<string> ShowAsync(string[] buttons, string title, string message)
        {
            Write(title, message);
            return Task.FromResult(buttons.FirstOrDefault() ?? string.Empty);
        }

        public Task<string> ShowAsync(string[] buttons, string title, string message, DoNotShowAgainOptions doNotShowAgainOption)
        {
            Write(title, message);
            return Task.FromResult(buttons.FirstOrDefault() ?? string.Empty);
        }

        private static void Write(string title, string message)
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                Console.Error.WriteLine(title);
            }
            if (!string.IsNullOrWhiteSpace(message))
            {
                Console.Error.WriteLine(message);
            }
        }
    }
}
