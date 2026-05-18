namespace Partio.Server.Models
{
    /// <summary>
    /// Exception wrapper that preserves partial process diagnostics.
    /// </summary>
    public class ProcessCellException : Exception
    {
        /// <summary>
        /// Partial or completed process result captured before the failure surfaced.
        /// </summary>
        public ProcessCellResult Result { get; }

        /// <summary>
        /// Initialize a new exception wrapper.
        /// </summary>
        public ProcessCellException(string message, Exception innerException, ProcessCellResult result)
            : base(message, innerException)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }
    }
}
