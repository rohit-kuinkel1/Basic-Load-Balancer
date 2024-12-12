namespace LoadBalancer.Exceptions
{
    /// <summary>
    /// Exception thrown when a null argument is passed to a method that requires a non-null argument.
    /// </summary>
    public class NullException : LoadBalancerException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NullException"/> class with a specified parameter name.
        /// </summary>
        /// <param name="message">The nameof the provided param.</param>
        public NullException(string arg)
            : base($"Argument {arg} was null when it was not supposed to be.", "LB-NULL-001")
        {
        }

        /// <summary>
        /// Default constructor for <see cref="NullException"/>
        /// </summary>
        public NullException()
            : base($"Argument cannot be null.", "LB-NULL-001")
        {
        }

        /// <summary>
        /// Detailed parameterized constructor with <paramref name="type"/>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        public NullException(string message, Type type)
            : base(message, type, "LB-NULL-001")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NullException"/> class with a specified message and inner exception.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public NullException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
