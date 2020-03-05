namespace SACModuleBase.Enums.Captcha
{
    public enum CaptchaStatus
    {
        /// <summary>
        /// Will use another module if possible
        /// </summary>
        CannotSolve = -1,
        /// <summary>
        /// Failed to load captcha/exception/etc...
        /// </summary>
        Failed,
        /// <summary>
        /// Solved
        /// </summary>
        Success,
        /// <summary>
        /// Failed to solve, but you can retry
        /// </summary>
        RetryAvailable
    }
}
