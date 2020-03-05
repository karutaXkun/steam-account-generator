using SACModuleBase.Models.Capcha;
using System;
using System.Windows.Forms;

namespace SteamAccCreator.Interfaces
{
    public interface ICaptchaDialog : IDisposable
    {
        DialogResult ShowDialog(out CaptchaResponse solution);
    }
}
