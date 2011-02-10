﻿//    XSDDiagram - A XML Schema Definition file viewer
//    Copyright (C) 2006-2011  Regis COSNIER
//
//    This program is free software; you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation; either version 2 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program; if not, write to the Free Software
//    Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Xml.Serialization;

namespace XSDDiagram
{
    public class Schema
    {
        private static XmlSerializer schemaSerializer = new XmlSerializer(typeof(XMLSchema.schema));

        private List<XSDObject> elements = new List<XSDObject>();
        private Hashtable hashtableElementsByName = new Hashtable();
        private Hashtable hashtableAttributesByName = new Hashtable();
        private XSDObject firstElement = null;
        private List<string> loadError = new List<string>();
        private List<string> listOfXsdFilename = new List<string>();

        public Schema()
        {
            this.hashtableElementsByName[""] = null;
            this.hashtableAttributesByName[""] = null;

            schemaSerializer.UnreferencedObject += new UnreferencedObjectEventHandler(schemaSerializer_UnreferencedObject);
            schemaSerializer.UnknownNode += new XmlNodeEventHandler(schemaSerializer_UnknownNode);
            schemaSerializer.UnknownElement += new XmlElementEventHandler(schemaSerializer_UnknownElement);
            schemaSerializer.UnknownAttribute += new XmlAttributeEventHandler(schemaSerializer_UnknownAttribute);
        }

        public IList<XSDObject> Elements { get { return elements; } }
        public Hashtable ElementsByName { get { return hashtableElementsByName; } }
        public Hashtable AttributesByName { get { return hashtableAttributesByName; } }
        public XSDObject FirstElement { get { return firstElement; } set { firstElement = value; } }
        public IList<string> LoadError { get { return loadError; } }
        public IList<string> XsdFilenames { get { return listOfXsdFilename; } }

        public void LoadSchema(string fileName)
        {
            this.firstElement = null;
            this.elements.Clear();
            this.hashtableElementsByName.Clear();
            this.hashtableElementsByName[""] = null;
            this.hashtableAttributesByName.Clear();
            this.hashtableAttributesByName[""] = null;
            this.loadError.Clear();
            this.listOfXsdFilename.Clear();

            ImportSchema(fileName);

        }

        private void ImportSchema(string fileName)
        {
            FileStream fileStream = null;
            try
            {
                fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                this.loadError.Clear();
                XMLSchema.schema schemaDOM = (XMLSchema.schema)schemaSerializer.Deserialize(fileStream);

                this.listOfXsdFilename.Add(fileName);

                ParseSchema(fileName, schemaDOM);
            }
            catch (IOException ex)
            {
                this.loadError.Add(ex.Message);
            }
            catch (NotSupportedException ex)
            {
                this.loadError.Add(ex.Message + " (" + fileName + ")");
            }
            catch (InvalidOperationException ex)
            {
                this.loadError.Add(ex.Message + "\r\n" + ex.InnerException.Message);
            }
            finally
            {
                if (fileStream != null)
                    fileStream.Close();
            }
        }

        private void ParseSchema(string fileName, XMLSchema.schema schemaDOM)
        {
            string basePath = Path.GetDirectoryName(fileName);
            if (schemaDOM.Items != null)
            {
                foreach (XMLSchema.openAttrs openAttrs in schemaDOM.Items)
                {
                    string loadedFileName = "";
                    string schemaLocation = "";

                    if (openAttrs is XMLSchema.include)
                    {
                        XMLSchema.include include = openAttrs as XMLSchema.include;
                        if (include.schemaLocation != null)
                            schemaLocation = include.schemaLocation;
                    }
                    else if (openAttrs is XMLSchema.import)
                    {
                        XMLSchema.import import = openAttrs as XMLSchema.import;
                        if (import.schemaLocation != null)
                            schemaLocation = import.schemaLocation;
                    }

                    if (!string.IsNullOrEmpty(schemaLocation))
                    {
                        loadedFileName = basePath + Path.DirectorySeparatorChar + schemaLocation.Replace('/', Path.DirectorySeparatorChar);

                        string url = schemaLocation.Trim();
                        if (url.IndexOf("http://") == 0 || url.IndexOf("https://") == 0)
                        {
                            Uri uri = new Uri(url);
                            if (uri.Segments.Length > 0)
                            {
                                string fileNameToImport = uri.Segments[uri.Segments.Length - 1];
                                loadedFileName = basePath + Path.DirectorySeparatorChar + fileNameToImport;
                                if (!File.Exists(loadedFileName))
                                {
                                    WebClient webClient = new WebClient();
                                    //webClient.Credentials = new System.Net.NetworkCredential("username", "password");
                                    try
                                    {
                                        webClient.DownloadFile(uri, loadedFileName);
                                    }
                                    catch (WebException)
                                    {
                                        this.loadError.Add("Cannot load the dependency: " + uri.ToString());
                                        loadedFileName = null;
                                    }
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(loadedFileName))
                        ImportSchema(loadedFileName);
                }
            }

            string nameSpace = schemaDOM.targetNamespace;

            if (schemaDOM.Items1 != null)
            {
                foreach (XMLSchema.openAttrs openAttrs in schemaDOM.Items1)
                {
                    if (openAttrs is XMLSchema.element)
                    {
                        XMLSchema.element element = openAttrs as XMLSchema.element;
                        XSDObject xsdObject = new XSDObject(fileName, element.name, nameSpace, "element", element);
                        this.hashtableElementsByName[xsdObject.FullName] = xsdObject;

                        if (this.firstElement == null)
                            this.firstElement = xsdObject;

                        elements.Add(xsdObject);
                    }
                    else if (openAttrs is XMLSchema.group)
                    {
                        XMLSchema.group group = openAttrs as XMLSchema.group;
                        XSDObject xsdObject = new XSDObject(fileName, group.name, nameSpace, "group", group);
                        this.hashtableElementsByName[xsdObject.FullName] = xsdObject;

                        elements.Add(xsdObject);
                    }
                    else if (openAttrs is XMLSchema.simpleType)
                    {
                        XMLSchema.simpleType simpleType = openAttrs as XMLSchema.simpleType;
                        XSDObject xsdObject = new XSDObject(fileName, simpleType.name, nameSpace, "simpleType", simpleType);
                        this.hashtableElementsByName[xsdObject.FullName] = xsdObject;

                        elements.Add(xsdObject);
                    }
                    else if (openAttrs is XMLSchema.complexType)
                    {
                        XMLSchema.complexType complexType = openAttrs as XMLSchema.complexType;
                        XSDObject xsdObject = new XSDObject(fileName, complexType.name, nameSpace, "complexType", complexType);
                        this.hashtableElementsByName[xsdObject.FullName] = xsdObject;

                        elements.Add(xsdObject);
                    }
                    else if (openAttrs is XMLSchema.attribute)
                    {
                        XMLSchema.attribute attribute = openAttrs as XMLSchema.attribute;
                        XSDAttribute xsdAttribute = new XSDAttribute(fileName, attribute.name, nameSpace, "attribute", attribute.@ref != null, attribute.@default, attribute.use.ToString(), attribute);
                        this.hashtableAttributesByName[xsdAttribute.FullName] = xsdAttribute;
                    }
                    else if (openAttrs is XMLSchema.attributeGroup)
                    {
                        XMLSchema.attributeGroup attributeGroup = openAttrs as XMLSchema.attributeGroup;
                        XSDAttributeGroup xsdAttributeGroup = new XSDAttributeGroup(fileName, attributeGroup.name, nameSpace, "attributeGroup", attributeGroup is XMLSchema.attributeGroupRef, attributeGroup);
                        this.hashtableAttributesByName[xsdAttributeGroup.FullName] = xsdAttributeGroup;
                    }
                }
            }
        }

        void schemaSerializer_UnknownAttribute(object sender, XmlAttributeEventArgs e)
        {
            this.loadError.Add("Unkonwn attribute (" + e.LineNumber + ", " + e.LinePosition + "): " + e.Attr.Name);
        }

        void schemaSerializer_UnknownElement(object sender, XmlElementEventArgs e)
        {
            this.loadError.Add("Unkonwn element (" + e.LineNumber + ", " + e.LinePosition + "): " + e.Element.Name);
        }

        void schemaSerializer_UnknownNode(object sender, XmlNodeEventArgs e)
        {
            this.loadError.Add("Unkonwn node (" + e.LineNumber + ", " + e.LinePosition + "): " + e.Name);
        }

        void schemaSerializer_UnreferencedObject(object sender, UnreferencedObjectEventArgs e)
        {
            this.loadError.Add("Unreferenced object: " + e.UnreferencedId);
        }
    }

    public class XSDObject
    {
        private string filename = "";
        private string name = "";
        private string nameSpace = "";
        private string type = "";
        private string fullNameType = "";
        private XMLSchema.openAttrs tag = null;

        public string Filename { get { return this.filename; } set { this.filename = value; } }
        public string Name { get { return this.name; } set { this.name = value; } }
        public string NameSpace { get { return this.nameSpace; } set { this.nameSpace = value; } }
        public string Type { get { return this.type; } set { this.type = value; } }
        public XMLSchema.openAttrs Tag { get { return this.tag; } set { this.tag = value; } }

        public string FullName { get { return this.nameSpace + ':' + this.fullNameType + ':' + this.name; } }

        public XSDObject(string filename, string name, string nameSpace, string type, XMLSchema.openAttrs tag)
        {
            this.filename = filename;
            this.name = name;
            this.nameSpace = (nameSpace == null ? "" : nameSpace);
            this.type = type;
            if (this.type == "simpleType" || this.type == "complexType")
                this.fullNameType = "type";
            else
                this.fullNameType = this.type;
            this.tag = tag;
        }

        public override string ToString()
        {
            return this.type + ": " + this.name + " (" + this.nameSpace + ")";
        }
    }

    public class XSDAttribute
    {
        private string filename = "";
        private string name = "";
        private string nameSpace = "";
        private string type = "";
        private bool isReference = false;
        private string defaultValue = "";
        private string use = "";
        private XMLSchema.attribute tag = null;

        public string Filename { get { return this.filename; } set { this.filename = value; } }
        public string Name { get { return this.name; } set { this.name = value; } }
        public string NameSpace { get { return this.nameSpace; } set { this.nameSpace = value; } }
        public string Type { get { return this.type; } set { this.type = value; } }
        public bool IsReference { get { return this.isReference; } set { this.isReference = value; } }
        public string DefaultValue { get { return this.defaultValue; } set { this.defaultValue = value; } }
        public string Use { get { return this.use; } set { this.use = value; } }
        public XMLSchema.attribute Tag { get { return this.tag; } set { this.tag = value; } }

        public string FullName { get { return this.nameSpace + ":attribute:" + this.name; } }

        public XSDAttribute(string filename, string name, string nameSpace, string type, bool isReference, string defaultValue, string use, XMLSchema.attribute attribute)
        {
            this.filename = filename;
            this.name = name;
            this.nameSpace = (nameSpace == null ? "" : nameSpace);
            this.type = type;
            this.isReference = isReference;
            this.defaultValue = defaultValue;
            this.use = use;
            this.tag = attribute;
        }

        public override string ToString()
        {
            return this.name + " (" + this.nameSpace + ")";
        }
    }

    public class XSDAttributeGroup
    {
        private string filename = "";
        private string name = "";
        private string nameSpace = "";
        private string type = "";
        private bool isReference = false;
        private XMLSchema.attributeGroup tag = null;

        public string Filename { get { return this.filename; } set { this.filename = value; } }
        public string Name { get { return this.name; } set { this.name = value; } }
        public string NameSpace { get { return this.nameSpace; } set { this.nameSpace = value; } }
        public string Type { get { return this.type; } set { this.type = value; } }
        public bool IsReference { get { return this.isReference; } set { this.isReference = value; } }
        public XMLSchema.attributeGroup Tag { get { return this.tag; } set { this.tag = value; } }

        public string FullName { get { return this.nameSpace + ":attributeGroup:" + this.name; } }

        public XSDAttributeGroup(string filename, string name, string nameSpace, string type, bool isReference, XMLSchema.attributeGroup attributeGroup)
        {
            this.filename = filename;
            this.name = name;
            this.nameSpace = (nameSpace == null ? "" : nameSpace);
            this.type = type;
            this.isReference = isReference;
            this.tag = attributeGroup;
        }

        public override string ToString()
        {
            return this.name + " (" + this.nameSpace + ")";
        }
    }
}
