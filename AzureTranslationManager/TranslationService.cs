using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzureTranslationManager
{
    public class TranslationService
    {
        private const int _maxRequestSize = 5000;
        private const string _defaultCategory = "general";
        private readonly TranslationClient Client;

        public static int MaxRequestSize => _maxRequestSize;
        public static string DefaultCategory => _defaultCategory;

        public TranslationService(TranslationClient client)
        {
            Client = client;
        }

        public string TranslateString(string text, string from, string to, ContentType contentType = ContentType.Plain)
        {
            var texts = new string[1];
            texts[0] = text;

            var results = TranslateArray(texts, from, to, contentType);
            return results[0];
        }

        public string[] TranslateArray(string[] texts, string from, string to, ContentType contentType = ContentType.Plain)
        {
            var results = TranslateV3Async(texts, from, to, DefaultCategory, contentType).Result;
            return results;
        }

        public async Task<string> TranslateStringAsync(string text, string from, string to, string category, ContentType contentType = ContentType.Plain)
        {
            var texts = new string[1];
            texts[0] = text;

            var results = await TranslateV3Async(texts, from, to, category, contentType).ConfigureAwait(false);

            try
            {
                return results[0];
            }
            catch (IndexOutOfRangeException)
            {
                // A translate call with an expired subscription causes a null return
                throw new TranslationException("expired subscription");
            }
        }

        private async Task<string[]> TranslateV3Async(string[] texts, string from, string to, string category, ContentType contentType = ContentType.Plain)
        {
            if (from == to) 
                return texts;

            var translateindividually = false;

            foreach (string text in texts)
            {
                if (text.Length >= MaxRequestSize) 
                    translateindividually = true;
            }

            if (translateindividually)
            {
                var resultList = new List<string>();
                foreach (string text in texts)
                {
                    var splitstring = await SplitStringAsync(text, from).ConfigureAwait(false);
                    var linetranslation = string.Empty;
                    
                    foreach (string innertext in splitstring)
                    {
                        var innertranslation = await TranslateStringAsync(innertext, from, to, category, contentType).ConfigureAwait(false);
                        linetranslation += innertranslation;
                    }

                    resultList.Add(linetranslation);
                }
                return resultList.ToArray();
            }
            else
            {
                return await TranslateV3AsyncInternal(texts, from, to, category, contentType).ConfigureAwait(false);
            }
        }

        private async Task<string[]> TranslateV3AsyncInternal(string[] texts, string from, string to, string category, ContentType contentType)
        {
            var resultList = new List<string>();

            var requestUri = "translate?api-version=3.0&from=" + from + "&to=" + to + "&category=" + category;
            if (contentType == ContentType.Html)
                requestUri += "&textType=HTML";

            var inputs = texts.Select(text => new { Text = text });
            var requestBody = JsonConvert.SerializeObject(inputs);
            var responseBody = await Client.Request(requestUri, requestBody);
            if (!string.IsNullOrEmpty(responseBody))
            {
                JArray jArray;
                try
                {
                    jArray = JArray.Parse(responseBody);
                }
                catch (Exception)
                {
                    System.Diagnostics.Debug.WriteLine(responseBody);
                    throw;
                }
                foreach (JObject result in jArray)
                {
                    var text = (string)result.SelectToken("translations[0].text");
                    resultList.Add(text);
                }
            }

            return resultList.ToArray();
        }

        private async Task<List<string>> SplitStringAsync(string text, string languagecode)
        {
            var resultList = new List<string>();
            int previousBoundary = 0;
            if (text.Length <= MaxRequestSize)
            {
                resultList.Add(text);
            }
            else
            {
                while (previousBoundary <= text.Length)
                {
                    int boundary = await LastSentenceBreak(text.Substring(previousBoundary), languagecode).ConfigureAwait(false);
                    if (boundary == 0) 
                        break;

                    resultList.Add(text.Substring(previousBoundary, boundary));
                    previousBoundary += boundary;
                }
                resultList.Add(text.Substring(previousBoundary));
            }
            return resultList;
        }

        private async Task<int> LastSentenceBreak(string text, string languagecode)
        {
            var sum = 0;
            var breakSentenceResult = await BreakSentencesAsync(text, languagecode).ConfigureAwait(false);
            for (int i = 0; i < breakSentenceResult.Count - 1; i++) 
                sum += breakSentenceResult[i];

            return sum;
        }

        public async Task<List<int>> BreakSentencesAsync(string text, string languagecode)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(text)) 
                return null;

            var resultList = new List<int>();

            var requestUri = "breaksentence?api-version=3.0&language=" + languagecode;
            var inputs = new object[] { new { Text = text.Substring(0, (text.Length < MaxRequestSize) ? text.Length : MaxRequestSize) } };
            var requestBody = JsonConvert.SerializeObject(inputs);
            var responseBody = await Client.Request(requestUri, requestBody);
            
            var deserializedOutput = JsonConvert.DeserializeObject<BreakSentenceResult[]>(responseBody);
            foreach (BreakSentenceResult o in deserializedOutput)
            {
                resultList = o.SentLen.ToList();
            }

            return resultList;
        }

        private class BreakSentenceResult
        {
            public int[] SentLen { get; set; }

            public DetectedLanguage DetectedLanguage { get; set; }
        }

        private class DetectedLanguage
        {
            public string Language { get; set; }

            public float Score { get; set; }
        }
    }

    public enum ContentType
    {
        Plain,
        Html
    }
}
