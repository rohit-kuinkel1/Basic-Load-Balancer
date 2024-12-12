namespace LoadBalancer.Exceptions
{
    /// <summary>
    /// Exception thrown when a fatal operation cannot continue.
    /// </summary>
    internal class IllegalOperation : LoadBalancerException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IllegalOperation"/> class.
        /// </summary>
        public IllegalOperation()
            : base("Cannot continue because of a fatal operation.")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IllegalOperation"/> class with a specified message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public IllegalOperation(string message)
            : base(message, "LB-ILLGL-001")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IllegalOperation"/> class with a specified message and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public IllegalOperation(string message, Exception innerException)
            : base(message, "LB-ILLGL-001", innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IllegalOperation"/> class with a specified message, error code, and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="errorCode">The error code associated with the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public IllegalOperation(string message, string errorCode, Exception innerException)
            : base(message, errorCode, innerException)
        {
        }
    }
}