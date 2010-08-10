﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using TidyNet;
using Composite.Xml;


namespace Composite.WebClient.Services.WysiwygEditor
{
    /// <exclude />
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)] 
    public class TidyHtmlResult
    {
        public XDocument Output { get; set; }
        public string ErrorSummary { get; set; }
    }

    /// <summary>
    /// Summary description for HtmlTidyServices
    /// </summary>
    /// <exclude />
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)] 
    public static class MarkupTransformationServices
    {
        /// <summary>
        /// Repairs an html fragment (makes it Xhtml) and executes a transformation on it.
        /// </summary>
        /// <param name="html">The html to repair</param>
        /// <param name="xsltPath">The path to the XSLT to use for transformation</param>
        /// <param name="xsltParameters"></param> 
        /// <param name="errorSummary">out value - warnings generated while repairing the html</param>
        /// <returns></returns>
        public static XDocument RepairXhtmlAndTransform(string html, string xsltPath, Dictionary<string, string> xsltParameters, out string errorSummary)
        {
            TidyHtmlResult tidyHtmlResult = MarkupTransformationServices.TidyHtml(html);

            errorSummary = tidyHtmlResult.ErrorSummary;
            XNode tidiedXhtml = tidyHtmlResult.Output;
            XDocument outputDocument = new XDocument();

            XslCompiledTransform xslt = XsltServices.GetCompiledXsltTransform(xsltPath);

            using (XmlWriter writer = outputDocument.CreateWriter())
            {
                using (XmlReader reader = tidiedXhtml.CreateReader())
                {
                    if (xsltParameters != null && xsltParameters.Count > 0)
                    {
                        XsltArgumentList xsltArgumentList = new XsltArgumentList();
                        foreach (var xsltParameter in xsltParameters)
                        {
                            xsltArgumentList.AddParam(xsltParameter.Key, "", xsltParameter.Value );
                        }
                        xslt.Transform(reader, xsltArgumentList, writer);
                    }
                    else
                    {
                        xslt.Transform(reader, writer);
                    }
                }
            }

            return outputDocument;
        }


        /// <summary>
        /// Repairs an html fragment (makes it Xhtml) and executes a transformation on it.
        /// </summary>
        /// <param name="xml">The xml to repair</param>
        /// <param name="xsltPath">The path to the XSLT to use for transformation</param>        
        /// <returns></returns>
        public static XDocument RepairXmlAndTransform(string xml, string xsltPath)
        {
            XDocument tidiedXml = MarkupTransformationServices.TidyXml(xml);

            XDocument outputDocument = new XDocument();

            XslCompiledTransform xslt = XsltServices.GetCompiledXsltTransform(xsltPath);

            using (XmlWriter writer = outputDocument.CreateWriter())
            {
                using (XmlReader reader = tidiedXml.CreateReader())
                {
                    xslt.Transform(reader, writer);
                }
            }

            return outputDocument;
        }


        /// <summary>
        /// Cleans HTML documents or fragments into XHTML conformant markup
        /// </summary>
        /// <param name="htmlMarkup">The html to clean</param>
        /// <returns>A fully structured XHTML document, incl. html, head and body elements.</returns>
        public static TidyHtmlResult TidyHtml(string htmlMarkup)
        {
            byte[] htmlByteArray = Encoding.UTF8.GetBytes(htmlMarkup);

            Tidy tidy = GetXhtmlConfiguredTidy();

            List<string> namespacePrefixedElementNames = LocateNamespacePrefixedElementNames(htmlMarkup);
            Dictionary<string, string> namespacePrefixToUri = LocateNamespacePrefixToUriDeclarations(htmlMarkup);
            List<string> badNamespacePrefixedElementNames = namespacePrefixedElementNames.Where(s => namespacePrefixToUri.Where(d => s.StartsWith(d.Key)).Any() == false).ToList();
            AllowNamespacePrefixedElementNames(tidy, namespacePrefixedElementNames);

            TidyMessageCollection tidyMessages = new TidyMessageCollection();
            string xhtml = "";

            using (MemoryStream inputStream = new MemoryStream(htmlByteArray))
            {
                using (MemoryStream outputStream = new MemoryStream())
                {
                    tidy.Parse(inputStream, outputStream, tidyMessages);
                    outputStream.Position = 0;
                    StreamReader sr = new StreamReader(outputStream);
                    xhtml = sr.ReadToEnd();
                }
            }

            if (tidyMessages.Errors > 0)
            {
                StringBuilder errorMessageBuilder = new StringBuilder();
                foreach (TidyMessage message in tidyMessages)
                {
                    if (message.Level == MessageLevel.Error)
                        errorMessageBuilder.AppendLine(message.ToString());
                }
                throw new InvalidOperationException(string.Format("Failed to parse html:\n\n{0}", errorMessageBuilder.ToString()));
            }

            if (xhtml.IndexOf("<html>")>-1)
            {
                xhtml = xhtml.Replace("<html>", "<html xmlns=\"http://www.w3.org/1999/xhtml\">");
            }

            if (xhtml.IndexOf("xmlns=\"http://www.w3.org/1999/xhtml\"") == -1)
            {
                xhtml = xhtml.Replace("<html", "<html xmlns=\"http://www.w3.org/1999/xhtml\"");
            }

            xhtml = RemoveDuplicateAttributes(xhtml);
            xhtml = RemoveXmlDeclarations(xhtml);
            xhtml = UndoLowerCasingOfElementNames(xhtml, namespacePrefixedElementNames);
            xhtml = UndoLowerCasingOfNamespacePrefixes(xhtml, namespacePrefixToUri);
            StringBuilder messageBuilder = new StringBuilder();
            foreach (TidyMessage message in tidyMessages)
            {
                if (message.Level == MessageLevel.Warning)
                    messageBuilder.AppendLine(message.ToString());
            }

            List<string> badNamespacePrefixes = badNamespacePrefixedElementNames.Select(n => n.Substring(0, n.IndexOf(':'))).Union(LocateAttributeNamespacePrefixes(xhtml)).Distinct().ToList();

            XDocument outputResult;
            if (badNamespacePrefixedElementNames.Any())
            {
                string badDeclared = string.Join(" ", badNamespacePrefixes.Select(p => string.Format("xmlns:{0}='#bad'", p)).ToArray());
                XDocument badDoc = XDocument.Parse(string.Format("<root {0}>{1}</root>", badDeclared, xhtml));
                badDoc.Descendants().Attributes().Where(e => e.Name.Namespace == "#bad").Remove();
                badDoc.Descendants().Where(e => e.Name.Namespace == "#bad").Remove();
                outputResult = new XDocument(badDoc.Root.Descendants().First());
            }
            else
            {
                outputResult = XDocument.Parse(xhtml);
            }

            return new TidyHtmlResult { Output = outputResult, ErrorSummary = messageBuilder.ToString() };
        }


        /// <summary>
        /// Cleans HTML documents or fragments into XHTML conformant markup
        /// </summary>
        /// <param name="xmlMarkup">The html to clean</param>
        /// <returns></returns>
        public static XDocument TidyXml(string xmlMarkup)
        {
            try
            {
                return XhtmlDocument.Parse(xmlMarkup);
            }
            catch (Exception)
            {
                // take the slow road below...
            }

            byte[] xmlByteArray = Encoding.UTF8.GetBytes(xmlMarkup);

            Tidy tidy = GetXmlConfiguredTidy();

            List<string> namespacePrefixedElementNames = LocateNamespacePrefixedElementNames(xmlMarkup);
            AllowNamespacePrefixedElementNames(tidy, namespacePrefixedElementNames);

            TidyMessageCollection tidyMessages = new TidyMessageCollection();
            string xml = "";

            using (MemoryStream inputStream = new MemoryStream(xmlByteArray))
            {
                using (MemoryStream outputStream = new MemoryStream())
                {
                    tidy.Parse(inputStream, outputStream, tidyMessages);
                    outputStream.Position = 0;
                    StreamReader sr = new StreamReader(outputStream);
                    xml = sr.ReadToEnd();
                }
            }

            if (tidyMessages.Errors > 0)
            {
                StringBuilder errorMessageBuilder = new StringBuilder();
                foreach (TidyMessage message in tidyMessages)
                {
                    if (message.Level == MessageLevel.Error)
                        errorMessageBuilder.AppendLine(message.ToString());
                }
                throw new InvalidOperationException(string.Format("Failed to parse html:\n\n{0}", errorMessageBuilder.ToString()));
            }

            xml = RemoveDuplicateAttributes(xml);

            return XDocument.Parse(xml);
        }


        public static string OutputBodyDescendants(XDocument source)
        {
            string bodyInnerXhtml = "";

            XmlWriterSettings settings = CustomizedWriterSettings();
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (XmlWriter writer = XmlWriter.Create(memoryStream, settings))
                {
                    XNamespace xhtml = "http://www.w3.org/1999/xhtml";
                    XElement bodyElement = source.Descendants(xhtml + "body").First();

                    foreach (XNode element in bodyElement.Nodes())
                    {
                        element.WriteTo(writer);
                    }

                    writer.Flush();
                    memoryStream.Position = 0;
                    StreamReader sr = new StreamReader(memoryStream);
                    bodyInnerXhtml = sr.ReadToEnd();
                }
            }

            bodyInnerXhtml = bodyInnerXhtml.Replace(" xmlns=\"http://www.w3.org/1999/xhtml\"", "");

            Regex customNamespaceDeclarations = new Regex(@"<(.*?) xmlns:(?<prefix>[a-zA-Z0-9\._]*?)=""(?<uri>.*?)""([^>]*?)>", RegexOptions.Compiled);
            Dictionary<string, string> prefixToUriLookup = new Dictionary<string, string>();

            int lastLength = -1;
            while (bodyInnerXhtml.Length != lastLength)
            {
                lastLength = bodyInnerXhtml.Length;
                MatchCollection matchCollection = customNamespaceDeclarations.Matches(bodyInnerXhtml);

                foreach (Match match in matchCollection)
                {
                    string prefix = match.Groups["prefix"].Value;
                    if (prefixToUriLookup.ContainsKey(prefix) == false)
                    {
                        prefixToUriLookup.Add(prefix, match.Groups["uri"].Value);
                    }
                }

                bodyInnerXhtml = customNamespaceDeclarations.Replace(bodyInnerXhtml, "<$1$2>");
            }

            foreach (var prefixInfo in prefixToUriLookup)
            {
                Regex namespacePrefixedElement = new Regex("<(" + prefixInfo.Key + @":[a-zA-Z0-9\._]*?)([^>]*?)( ?/?)>", RegexOptions.Compiled);
                bodyInnerXhtml = namespacePrefixedElement.Replace(bodyInnerXhtml, "<$1$2 xmlns:" + prefixInfo.Key + "=\"" + prefixInfo.Value + "\"$3>");
            }

            return bodyInnerXhtml;
        }


        private static XmlWriterSettings CustomizedWriterSettings()
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.OmitXmlDeclaration = true;
            settings.ConformanceLevel = ConformanceLevel.Fragment;
            settings.CloseOutput = false;
            settings.Indent = true;
            settings.IndentChars = "\t";

            return settings;
        }


        private static string RemoveXmlDeclarations(string html)
        {
            Regex duplicateAttributesRegex = new Regex(@"<\?.*?>");

            int prevLength = -1;
            while (html.Length != prevLength)
            {
                prevLength = html.Length;
                html = duplicateAttributesRegex.Replace(html, "");
            }

            return html;
        }



        private static string RemoveDuplicateAttributes(string html)
        {
            Regex duplicateAttributesRegex = new Regex(@"<([^>]*?) (?<attributeName>\w*?)=(?<quote>""|')([^>]*?)(\k<quote>)([^>]*?) (\k<attributeName>)=(?<quote2>""|')([^>]*?)(\k<quote2>)([^>]*?)>");

            int prevLength = -1;
            while (html.Length != prevLength)
            {
                prevLength = html.Length;
                html = duplicateAttributesRegex.Replace(html, @"<$1 ${attributeName}=""$2""$4$8>");
            }

            return html;
        }



        private static string UndoLowerCasingOfNamespacePrefixes(string html, Dictionary<string, string> namespacePrefixToUri)
        {
            foreach (var namespaceMapping in namespacePrefixToUri.Where(f => f.Key.ToLower() != f.Key))
            {
                Regex tidyCasedElement = new Regex(@"<(.*?) xmlns:" + namespaceMapping.Key.ToLower() + @"=""" + namespaceMapping.Value + @"""(.*?)>");
                html = tidyCasedElement.Replace(html, "<$1 xmlns:" + namespaceMapping.Key + @"=""" + namespaceMapping.Value + @"""$2>");
            }

            return html;
        }



        private static string UndoLowerCasingOfElementNames(string html, List<string> elementNames)
        {
            foreach (string elementName in elementNames.Where(f => f.ToLower() != f))
            {
                Regex tidyCasedElement = new Regex(@"<(/?)" + elementName.ToLower() + @"(.*?)>");
                html = tidyCasedElement.Replace(html, "<$1" + elementName + "$2>");
            }

            return html;
        }



        private static Tidy GetXhtmlConfiguredTidy()
        {
            Tidy t = new Tidy();

            t.Options.RawOut = true;
            t.Options.TidyMark = false;

            t.Options.CharEncoding = CharEncoding.UTF8;
            t.Options.DocType = DocType.Omit;
            t.Options.WrapLen = 0;

            t.Options.BreakBeforeBR = true;
            t.Options.DropEmptyParas = false;
            t.Options.Word2000 = true;
            t.Options.MakeClean = false;
            t.Options.Xhtml = true;

            t.Options.QuoteNbsp = false;
            t.Options.NumEntities = true;

            return t;
        }



        private static Tidy GetXmlConfiguredTidy()
        {
            Tidy t = new Tidy();

            t.Options.RawOut = true;
            t.Options.TidyMark = false;
//            t.Options.XmlTags = true;

            t.Options.CharEncoding = CharEncoding.UTF8;
            t.Options.DocType = DocType.Omit;
            t.Options.WrapLen = 0;

            t.Options.Xhtml = false;
            t.Options.XmlOut = true;

            t.Options.QuoteNbsp = false;
            t.Options.NumEntities = true;

            return t;
        }



        private static void AllowNamespacePrefixedElementNames(Tidy tidy, List<string> elementNames)
        {
            foreach (string elementName in elementNames)
            {
                tidy.Options.AddTag(elementName.ToLower());
            }
        }



        private static List<string> LocateNamespacePrefixedElementNames(string htmlMarkup)
        {
            List<string> prefixedElementNames = new List<string>();

            Regex namespacePrefixedElement = new Regex(@"<([a-zA-Z0-9\._]*?):([a-zA-Z0-9\._]*)(.*?)(/?)>");
            MatchCollection matches = namespacePrefixedElement.Matches(htmlMarkup);

            foreach (Match match in matches)
            {
                string prefixedElementName = string.Format("{0}:{1}", match.Groups[1].Value, match.Groups[2].Value);
                if (prefixedElementNames.Contains(prefixedElementName) == false)
                {
                    prefixedElementNames.Add(prefixedElementName);
                }
            }
            return prefixedElementNames;
        }



        private static List<string> LocateAttributeNamespacePrefixes(string htmlMarkup)
        {
            List<string> prefixes = new List<string>();

            Regex elementsWithPrefixedAttributes = new Regex(@"<[^>]*? ([\w]):.*?>");
            MatchCollection matches = elementsWithPrefixedAttributes.Matches(htmlMarkup);

            foreach (Match match in matches)
            {
                string prefix = match.Groups[1].Value;
                if (prefixes.Contains(prefix) == false)
                {
                    prefixes.Add(prefix);
                }
            }
            return prefixes;
        }



        private static Dictionary<string, string> LocateNamespacePrefixToUriDeclarations(string htmlMarkup)
        {
            Dictionary<string, string> prefixToUri = new Dictionary<string, string>();
            Dictionary<string, string> lowerCasePrefixToUri = new Dictionary<string, string>();

            Regex elementWithNamespaceDeclaration = new Regex(@"<(.*?) xmlns:([a-zA-Z0-9\._]*)=""(.*?)""(.*?)(/?)>");
            MatchCollection matches = elementWithNamespaceDeclaration.Matches(htmlMarkup);

            foreach (Match match in matches)
            {
                string prefix = match.Groups[2].Value;
                string uri = match.Groups[3].Value;

                if (prefixToUri.ContainsKey(prefix) == false)
                {
                    prefixToUri.Add(prefix, uri);
                }
                else
                {
                    if (prefixToUri[prefix] != uri) throw new NotImplementedException(string.Format("The namespace prefix {0} is used to identify multiple namespaces. This may be legal XML but is not supported here", prefix));
                }
            }
            return prefixToUri;
        }




    }
}