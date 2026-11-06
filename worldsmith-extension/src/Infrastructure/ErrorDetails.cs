using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace WorldsmithExtension
{
    internal static class ErrorDetails
    {
        internal static string Message(Exception error)
        {
            var messages = new List<string>();
            Collect(error, messages);
            return String.Join(Environment.NewLine, messages.Distinct());
        }

        static void Collect(Exception error, List<string> messages)
        {
            var aggregate = error as AggregateException;
            if (aggregate != null)
            {
                foreach (var child in aggregate.InnerExceptions) Collect(child, messages);
                return;
            }
            // keep XML file/line info, skip reflection's wrapper errors
            if (!(error is TargetInvocationException) || error.InnerException == null)
                messages.Add(error.Message);
            if (error.InnerException != null) Collect(error.InnerException, messages);
        }
    }
}
