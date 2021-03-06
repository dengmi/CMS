﻿#region License
// 
// Copyright (c) 2013, Kooboo team
// 
// Licensed under the BSD License
// See the file LICENSE.txt for details.
// 
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;
using Kooboo.CMS.Common.Runtime.Dependency;
using System.Text.RegularExpressions;
using System.IO;

namespace Kooboo.CMS.Sites.View.WebProxy
{
    public interface IProxyHtmlFixer
    {
        IHtmlString Fix(string baseUri, string html, Func<string, bool, string> proxyUrlFunc);
    }
    [Dependency(typeof(IProxyHtmlFixer))]
    public class ProxyHtmlFixer : IProxyHtmlFixer
    {
        private Regex bodyRegex = new Regex("<body[^>]*>(.*?)<\\/body>", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);
        #region Fix
        public IHtmlString Fix(string baseUri, string rawHtml, Func<string, bool, string> proxyUrlFunc)
        {
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(rawHtml);

            HtmlNode headNode = null;
            HtmlNode bodyNode = null;

            foreach (var node in htmlDoc.DocumentNode.Descendants())
            {
                var nodeName = node.Name.ToLower();
                var elementName = node.Attributes["name"] == null ? "" : node.Attributes["name"].Value.ToLower();
                if (elementName == "__viewstate") { continue; }
                if (elementName == "__eventvalidation") { continue; }
                if (nodeName == "head")
                {
                    headNode = node;
                }
                if (nodeName == "body")
                {
                    bodyNode = node;
                }
                foreach (var attr in node.Attributes)
                {
                    if (IsW3CUrlAttribute(attr.Name.ToLower()) || attr.Value.StartsWith("/"))
                    {
                        var isStaticResource = IsStaticResource(node, attr);
                        if (isStaticResource)
                        {
                            attr.Value = GenerateAbsoluteUrl(baseUri, attr.Value);
                        }
                        else
                        {
                            var isForm = node.Name.ToLower() == "form";
                            attr.Value = proxyUrlFunc(attr.Value, isForm);
                        }
                    }
                }
            }
            string newHtml = "";
            if (bodyNode == null)
            {
                newHtml = htmlDoc.DocumentNode.InnerHtml;
            }
            else
            {
                if (headNode != null)
                {
                    newHtml = headNode.InnerHtml;
                }
                newHtml = newHtml + bodyNode.InnerHtml;
            }

            return new HtmlString(newHtml);
        }
        #endregion

        #region GetBodyNode
        protected virtual string GetBodyNode(string html)
        {
            var body = bodyRegex.Match(html);
            if (body == null)
            {
                return html;
            }
            else
            {
                return body.Groups[1].Value;
            }
        }
        #endregion

        #region UpdateUrl
        private bool IsStaticResource(HtmlNode node, HtmlAttribute attr)
        {
            var nodeName = node.Name.ToLower();
            if (nodeName == "link" || nodeName == "script" || nodeName == "img")
            {
                return true;
            }
            var extension = Path.GetExtension(attr.Value).ToLower();

            if (extension == ".js" || extension == ".css" || extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".ico" || extension == ".gif")
            {
                return true;
            }

            return false;
        }
        private static string GenerateAbsoluteUrl(string baseUri, string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return baseUri;
            }

            if (!url.StartsWith("#") && !url.StartsWith("javascript:"))
            {
                return new Uri(new Uri(baseUri), url).ToString();
            }
            else
            {
                return url;
            }
        }
        private void UpdateNodesUrl(IEnumerable<HtmlNode> htmlNodes, Func<string, bool, string> urlGenerator)
        {
            if (htmlNodes != null)
            {
                foreach (var item in htmlNodes)
                {
                    var rawUrl = GetUrl(item);
                    var url = urlGenerator(rawUrl, item.Name.ToLower() == "form");
                    SetUrl(item, url);
                }
            }
        }
        private bool IsW3CUrlAttribute(string attrName)
        {
            return attrName == "href" || attrName == "action" || attrName == "src";
        }

        private string GetUrl(HtmlNode htmlNode)
        {
            switch (htmlNode.Name.ToLower())
            {
                case "a":
                case "link":
                    return htmlNode.Attributes["href"].Value;
                case "form":
                    return htmlNode.Attributes["action"].Value;
                case "script":
                case "img":
                    return htmlNode.Attributes["src"].Value;
                default:
                    return "";
            }
        }
        private void SetUrl(HtmlNode htmlNode, string url)
        {
            switch (htmlNode.Name.ToLower())
            {
                case "a":
                case "link":
                    htmlNode.Attributes["href"].Value = url;
                    break;
                case "form":
                    htmlNode.Attributes["action"].Value = url;
                    break;
                case "script":
                case "img":
                    htmlNode.Attributes["src"].Value = url;
                    break;
                default:
                    break;
            }
        }
        #endregion
    }
}
