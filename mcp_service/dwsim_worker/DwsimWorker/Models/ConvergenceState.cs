namespace DwsimWorker.Models
{
    /// <summary>
    /// Represents the convergence state of a solver calculation.
    /// </summary>
    public enum ConvergenceState
    {
        /// <summary>
        /// Calculation has not been started.
        /// </summary>
        NotStarted,

        /// <summary>
        /// Calculation is currently in progress.
        /// </summary>
        InProgress,

        /// <summary>
        /// Calculation converged successfully.
        /// </summary>
        Converged,

        /// <summary>
        /// Calculation did not converge within iteration limits.
        /// </summary>
        NotConverged,

        /// <summary>
        /// Calculation encountered an error.
        /// </summary>
        Error
    }
}
