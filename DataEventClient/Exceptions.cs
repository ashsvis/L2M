using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataEventClient
{
    public static class Exceptions
    {
        private static readonly List<Type> FatalExceptions = new List<Type>
            {
                typeof (OutOfMemoryException),
                typeof (StackOverflowException),
//Ещё типы исключений, который по вашему мнению всегда являются фатальными
            };

        public static string FullMessage(this Exception ex)
        {
            var builder = new StringBuilder();
            while (ex != null)
            {
                builder.AppendFormat("{0}{1}", ex, Environment.NewLine);
                ex = ex.InnerException;
            }
            return builder.ToString();
        }

        public static void TryFilterCatch(Action tryAction, Func<Exception, bool> isRecoverPossible,
                                          Action handlerAction)
        {
            try
            {
                tryAction();
            }
            catch (Exception ex)
            {
                if (!isRecoverPossible(ex)) throw;
                handlerAction();
            }
        }

        public static void TryFilterCatch(Action tryAction, Func<Exception, bool> isRecoverPossible,
                                          Action<Exception> handlerAction)
        {
            try
            {
                tryAction();
            }
            catch (Exception ex)
            {
                if (!isRecoverPossible(ex))
                    throw;
                handlerAction(ex);
            }
        }

        public static bool NotFatal(this Exception ex)
        {
            return FatalExceptions.All(curFatal => ex.GetType() != curFatal);
        }

        public static bool IsFatal(this Exception ex)
        {
            return !NotFatal(ex);
        }
    }
}
