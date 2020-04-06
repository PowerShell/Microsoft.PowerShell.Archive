// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Diagnostics;
using System.Reflection;
using System.Management.Automation.Internal;
using System.Management.Automation;

namespace Microsoft.PowerShell.Archive
{
    /// <summary>
    /// A TraceSource is a representation of a System.Diagnostics.TraceSource instance
    /// that is used the the Monad components to produce trace output.
    /// </summary>
    /// <remarks>
    /// It is permitted to subclass <see cref="TraceSource"/>
    /// but there is no established scenario for doing this, nor has it been tested.
    /// </remarks>

    internal partial class TraceSource
    {



        /// <summary>
        /// Traces the Message and StackTrace properties of the exception
        /// and returns the new exception. This is not allowed to call other
        /// Throw*Exception variants, since they call this.
        /// </summary>
        /// <returns>Exception instance ready to throw.</returns>
        internal static PSNotSupportedException NewNotSupportedException()
        {
            string message = String.Format(Exceptions.NotSupported,
                new System.Diagnostics.StackTrace().GetFrame(0).ToString());
            var e = new PSNotSupportedException(message);

            return e;
        }

        /// <summary>
        /// Traces the Message and StackTrace properties of the exception
        /// and returns the new exception. This is not allowed to call other
        /// Throw*Exception variants, since they call this.
        /// </summary>
        /// <param name="paramName">
        /// The name of the parameter whose argument value was null
        /// </param>
        /// <returns>Exception instance ready to throw.</returns>
        internal static PSArgumentNullException NewArgumentNullException(string paramName)
        {
            if (string.IsNullOrEmpty(paramName))
            {
                throw new ArgumentNullException("paramName");
            }

            string message = String.Format(Exceptions.ArgumentNull, paramName);
            var e = new PSArgumentNullException(paramName, message);

            return e;
        }

        /// <summary>
        /// Traces the Message and StackTrace properties of the exception
        /// and returns the new exception. This variant allows the caller to
        /// specify alternate template text, but only in assembly S.M.A.Core.
        /// </summary>
        /// <param name="paramName">
        /// The name of the parameter whose argument value was invalid
        /// </param>
        /// <param name="resourceString">
        /// The template string for this error
        /// </param>
        /// <param name="args">
        /// Objects corresponding to {0}, {1}, etc. in the resource string
        /// </param>
        /// <returns>Exception instance ready to throw.</returns>
        internal static PSArgumentNullException NewArgumentNullException(
            string paramName, string resourceString, params object[] args)
        {
            if (string.IsNullOrEmpty(paramName))
            {
                throw NewArgumentNullException("paramName");
            }

            if (string.IsNullOrEmpty(resourceString))
            {
                throw NewArgumentNullException("resourceString");
            }

            string message = String.Format(resourceString, args);

            // Note that the paramName param comes first
            var e = new PSArgumentNullException(paramName, message);

            return e;
        }

        /// <summary>
        /// Traces the Message and StackTrace properties of the exception
        /// and returns the new exception. This variant uses the default
        /// ArgumentException template text. This is not allowed to call
        /// other Throw*Exception variants, since they call this.
        /// </summary>
        /// <param name="paramName">
        /// The name of the parameter whose argument value was invalid
        /// </param>
        /// <returns>Exception instance ready to throw.</returns>
        internal static PSArgumentException NewArgumentException(string paramName)
        {
            if (string.IsNullOrEmpty(paramName))
            {
                throw new ArgumentNullException("paramName");
            }

            string message = String.Format(Exceptions.Argument, paramName);
            // Note that the message param comes first
            var e = new PSArgumentException(message, paramName);

            return e;
        }

        /// <summary>
        /// Traces the Message and StackTrace properties of the exception
        /// and returns the new exception. This variant allows the caller to
        /// specify alternate template text, but only in assembly S.M.A.Core.
        /// </summary>
        /// <param name="paramName">
        /// The name of the parameter whose argument value was invalid
        /// </param>
        /// <param name="resourceString">
        /// The template string for this error
        /// </param>
        /// <param name="args">
        /// Objects corresponding to {0}, {1}, etc. in the resource string
        /// </param>
        /// <returns>Exception instance ready to throw.</returns>
        internal static PSArgumentException NewArgumentException(
            string paramName, string resourceString, params object[] args)
        {
            if (string.IsNullOrEmpty(paramName))
            {
                throw NewArgumentNullException("paramName");
            }

            if (string.IsNullOrEmpty(resourceString))
            {
                throw NewArgumentNullException("resourceString");
            }

            string message = String.Format(resourceString, args);

            // Note that the message param comes first
            var e = new PSArgumentException(message, paramName);

            return e;
        }

    }

}