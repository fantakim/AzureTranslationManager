using System;
using System.Net;

namespace AzureTranslationManager
{
    public class TranslationException : Exception
    {
        public HttpStatusCode HttpStatusCode { get; set; }

        public TranslationException()
        {
        }

        public TranslationException(string message)
            : base(message)
        {
        }

        public TranslationException(string message, Exception err)
            : base(message, err)
        {
        }

        public TranslationException(HttpStatusCode httpStatusCode, string message)
            : base(message)
        {
            this.HttpStatusCode = httpStatusCode;
        }
    }
}
