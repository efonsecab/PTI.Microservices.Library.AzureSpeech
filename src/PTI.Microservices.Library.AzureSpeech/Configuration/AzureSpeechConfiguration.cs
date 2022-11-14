using System;
using System.Collections.Generic;
using System.Text;

namespace PTI.Microservices.Library.Configuration
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class AzureSpeechConfiguration
    {
        public string Key { get; set; }
        /// <summary>
        /// Check region identifiers here: https://docs.microsoft.com/en-us/azure/cognitive-services/speech-service/regions
        /// </summary>
        public string Region { get; set; }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
