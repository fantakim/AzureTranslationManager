using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AzureTranslationManager
{
    public class TranslationManager
    {
        private TranslationConfiguration Configuration;

        public TranslationManager(TranslationConfiguration configuration)
        {
            Configuration = configuration;
        }

        public string Translate(string content, string from, string to, ContentType contentType)
        {
            var client = new TranslationClient(Configuration);
            var service = new TranslationService(client);

            if (contentType == ContentType.Plain)
                return TextTranslate(service, content, from, to);
            else if (contentType == ContentType.Html)
                return HtmlTranslate(service, content, from, to);
            else
                throw new TranslationException("Content-type not supported");
        }

        private string TextTranslate(TranslationService service, string content, string from, string to)
        {
            var sb = new StringBuilder();
            var texts = Regex.Split(content, "\r\n|\r|\n");
            var batches = SplitList(texts, 100, TranslationService.MaxRequestSize);
            foreach (var batch in batches)
            {
                var translated = service.TranslateArray(batch.ToArray(), from, to);
                sb.Append(StringArrayToString(translated));
            }

            return sb.ToString();
        }

        private string HtmlTranslate(TranslationService service, string content, string from, string to)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(content);

            var title = htmlDoc.DocumentNode.SelectSingleNode("//head//title");
            if (title != null)
                title.InnerHtml = service.TranslateString(title.InnerHtml, from, to, ContentType.Html);

            var body = htmlDoc.DocumentNode.SelectSingleNode("//body") ?? htmlDoc.DocumentNode;
            if (body != null)
            {
                if (body.InnerHtml.Length < TranslationService.MaxRequestSize)
                {
                    body.InnerHtml = service.TranslateString(body.InnerHtml, from, to, ContentType.Html);
                }
                else
                {
                    var nodes = new List<HtmlNode>();
                    AddNodes(body.FirstChild, ref nodes);

                    try
                    {
                        foreach (var node in nodes)
                        {
                            node.InnerHtml = service.TranslateString(node.InnerHtml, from, to, ContentType.Html);
                        }
                    }
                    catch (Exception) { }
                }
            }

            return htmlDoc.DocumentNode.OuterHtml;
        }

        private string StringArrayToString(string[] array)
        {
            var builder = new StringBuilder();
            foreach (string value in array)
            {
                builder.Append(value);
                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static List<List<T>> SplitList<T>(IEnumerable<T> values, int groupSize, int maxSize)
        {
            var result = new List<List<T>>();
            var valueList = values.ToList();
            var startIndex = 0;
            var count = valueList.Count;

            while (startIndex < count)
            {
                int elementCount = (startIndex + groupSize > count) ? count - startIndex : groupSize;
                while (true)
                {
                    var aggregatedSize =
                        valueList.GetRange(startIndex, elementCount)
                            .Aggregate(
                                new StringBuilder(),
                                (s, i) => s.Length < maxSize ? s.Append(i) : s,
                                s => s.ToString())
                            .Length;
                    if (aggregatedSize >= maxSize)
                    {
                        if (elementCount == 1) break;
                        elementCount = elementCount - 1;
                    }
                    else
                    {
                        break;
                    }
                }

                result.Add(valueList.GetRange(startIndex, elementCount));
                startIndex += elementCount;
            }

            return result;
        }

        private static void AddNodes(HtmlNode rootnode, ref List<HtmlNode> nodes)
        {
            try
            {
                // do not translate.
                var skipNodes = new [] { "script", "#text", "code", "col", "colgroup", "embed", "em", "#comment", "image", "map", "media", "meta", "source", "xml" };
                HtmlNode child = rootnode;
                while (child != null && child != rootnode.LastChild)
                {
                    if (!skipNodes.Contains(child.Name.ToLowerInvariant()))
                    {
                        if (child.InnerHtml.Length > TranslationService.MaxRequestSize)
                        {
                            AddNodes(child.FirstChild, ref nodes);
                        }
                        else
                        {
                            if (child.InnerHtml.Trim().Length != 0) nodes.Add(child);
                        }
                    }
                    child = child.NextSibling;
                }
            }
            catch (Exception) { }
        }
    }
}
