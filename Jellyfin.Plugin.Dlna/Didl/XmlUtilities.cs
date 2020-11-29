using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Linq;

namespace Jellyfin.Plugin.Dlna.Didl
{
    /// <summary>
    /// Non-strict XML parser. Handles invalid XML responses.
    ///
    /// Parses an XML style document into a dictionary.
    /// </summary>
    public static class XmlUtilities
    {
        /// <summary>
        /// Xml Property callback for use./>.
        /// </summary>
        /// <param name="name">Name of property.</param>
        /// <param name="value">Value of property.</param>
        /// <param name="el">Actual XElement element.</param>
        /// <param name="depth">Level of the element in the tree.</param>
        /// <returns>Result of the operation.</returns>
        private delegate bool? XmlCallback(string name, string value, XElement el, int depth);

        /// <summary>
        /// Only UrlEncodes the query string part of a url.
        /// </summary>
        /// <param name="url">Url to encode.</param>
        /// <returns>The encoded url or an empty string if <paramref name="url"/> was null."/>.</returns>
        public static string EncodeUrl(string? url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }

            int i = url.IndexOf('?');

            if (i == -1)
            {
                return url;
            }

            return url[0..(i + 1)] + HttpUtility.UrlEncode(url[(i + 1)..]);
        }

        /// <summary>
        /// Parses a xml string using .dotnet's xElement as loosely as possible (without character checking and validation checks).
        /// </summary>
        /// <param name="xml">String to parse.</param>
        /// <param name="document"><see cref="XDocument"/> if the function returns true.</param>
        /// <returns><c>True</c> is successfully parsed.</returns>
        public static bool ParseXml(string xml, [NotNullWhen(true)] out XElement? document)
        {
            var settings = new XmlReaderSettings
            {
                ConformanceLevel = ConformanceLevel.Fragment,
                ValidationType = ValidationType.None,
                CheckCharacters = false,
                IgnoreProcessingInstructions = true,
                IgnoreComments = true
            };

            using var xr = XmlReader.Create(new StringReader(xml), settings);
            try
            {
                document = XElement.Load(xr);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                document = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Parses XML into a dictionary object. Designed for responses with unique elements.
        /// DOES NOT SUPPORT MULTIPLE RECORDS.
        /// </summary>
        /// <param name="xml">Xml to parse.</param>
        /// <param name="result">A <see cref="Dictionary{TKey, TValue}"/> of elements and attributes. Attributes are pre-pended with the element name.</param>
        /// <param name="dictionary">An optional <see cref="Dictionary{TKey, TValue}"/> to store values.</param>
        /// <returns>Non null if successful.</returns>
        public static bool XmlToDictionary(string xml, [NotNullWhen(true)] out Dictionary<string, string>? result, Dictionary<string, string>? dictionary = null)
        {
            result = null;
            if (!ParseXml(xml, out var element))
            {
                return false;
            }

            var dict = dictionary ?? new Dictionary<string, string>();
            if (element.ForEach((_, value, el, depth) =>
                {
                    if (string.Equals(el.Name.LocalName, "LastChange", StringComparison.Ordinal))
                    {
                        return XmlToDictionary(value, out var lastChanged, dict);
                    }
                    else if (!el.HasElements)
                    {
                        // Don't use name here as we want to keep case.
                        var attrib = el.FirstAttribute;
                        if (attrib != null)
                        {
                            if (!(attrib.Value.EndsWith("IMPLEMENTED", StringComparison.Ordinal) && attrib.Value.StartsWith("NOT", StringComparison.Ordinal)))
                            {
                                dict[el.Name.LocalName + '.' + attrib.Name.LocalName] = attrib.Value;
                            }
                        }

                        if (string.IsNullOrEmpty(value) && depth > 3)
                        {
                            // we only need to keep the response values.
                            return true;
                        }

                        dict[el.Name.LocalName] = value;
                        return true;
                    }

                    dict[el.Name.LocalName] = string.Empty;
                    return false;
                }) != null)
            {
                result = dict;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Recursively calls each element.
        /// </summary>
        /// <param name="xml"><see cref="XElement"/> object to trawl.</param>
        /// <param name="callback">Delegate method to call with each element.</param>
        /// <returns>Non null if successful.</returns>
        private static bool? ForEach(this XElement xml, XmlCallback callback)
        {
            bool? result;

            if (!xml.HasElements && callback.Invoke(xml.Name.LocalName.ToLowerInvariant(), xml.Value, xml, 0) == null)
            {
                // Exit on an error
                return null;
            }

            return InnerForEach(xml, 0);

            bool? InnerForEach(XElement x, int depth)
            {
                foreach (var el in x.Elements())
                {
                    result = callback.Invoke(el.Name.LocalName.ToLowerInvariant(), el.Value, el, depth);
                    if (result == null)
                    {
                        // Exit on an error.
                        return null;
                    }

                    if (result == true)
                    {
                        // element processed successfully, so don't process sub-elements.
                        continue;
                    }

                    if (el.HasElements && InnerForEach(el, depth + 1) == null)
                    {
                        // exit on an error.
                        return null;
                    }
                }

                return true;
            }
        }
    }
}
