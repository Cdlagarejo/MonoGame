﻿// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Xml;
using MGCB;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Graphics;
using PathHelper = MonoGame.Framework.Content.Pipeline.Builder.PathHelper;

namespace MonoGame.Tools.Pipeline
{
    /// <summary>
    /// The pipline project and helper methods.
    /// </summary>
    /// <remarks>
    /// NOTE: This class should never have any dependancy on the 
    /// controller or view... it is only the data "model".
    /// </remarks>
    class PipelineProject : IProjectItem
    {        
        #region CommandLineParameters

        private readonly List<ContentItem> _content = new List<ContentItem>();

        [Browsable(false)]
        public ReadOnlyCollection<ContentItem> ContentItems { get; private set; }

        [Browsable(false)]
        public string FilePath { get; set; }

        [CommandLineParameter(
            Name = "outputDir",
            ValueName = "directoryPath",
            Description = "The directory where all content is written.")]
        public string OutputDir { get; set; }

        [CommandLineParameter(
            Name = "intermediateDir",
            ValueName = "directoryPath",
            Description = "The directory where all intermediate files are written.")]
        public string IntermediateDir { get; set; }

        [TypeConverter(typeof(AssemblyReferenceListConverter))]
        [CommandLineParameter(
            Name = "reference",
            ValueName = "assemblyNameOrFile",
            Description = "Adds an assembly reference for resolving content importers, processors, and writers.")]
        public List<string> References { get; private set; }

        [CommandLineParameter(
            Name = "platform",
            ValueName = "targetPlatform",
            Description = "Set the target platform for this build.  Defaults to Windows.")]
        public TargetPlatform Platform { get; set; }

        [CommandLineParameter(
            Name = "profile",
            ValueName = "graphicsProfile",
            Description = "Set the target graphics profile for this build.  Defaults to HiDef.")]
        public GraphicsProfile Profile { get; set; }

        [CommandLineParameter(
            Name = "config",
            ValueName = "string",
            Description = "The optional build config string from the build system.")]
        public string Config { get; set; }

        [CommandLineParameter(
            Name = "importer",
            ValueName = "className",
            Description = "Defines the class name of the content importer for reading source content.")]
        public string Importer;

        private string _processor;

        [Browsable(false)]
        [CommandLineParameter(
            Name = "processor",
            ValueName = "className",
            Description = "Defines the class name of the content processor for processing imported content.")]
        public string Processor
        {
            get { return _processor; }
            set
            {
                _processor = value;
                _processorParams.Clear();
            }
        }

        private readonly OpaqueDataDictionary _processorParams = new OpaqueDataDictionary();

        [CommandLineParameter(
            Name = "processorParam",
            ValueName = "name=value",
            Description = "Defines a parameter name and value to set on a content processor.")]
        public void AddProcessorParam(string nameAndValue)
        {
            var keyAndValue = nameAndValue.Split('=');
            if (keyAndValue.Length != 2)
            {
                // Do we error out or something?
                return;
            }

            _processorParams.Remove(keyAndValue[0]);
            _processorParams.Add(keyAndValue[0], keyAndValue[1]);
        }

        [CommandLineParameter(
            Name = "build",
            ValueName = "sourceFile",
            Description = "Build the content source file using the previously set switches and options.")]
        public void OnBuild(string sourceFile)
        {
            // Make sure the source file is relative to the project.
            var projectDir = Location + "\\";
            sourceFile = PathHelper.GetRelativePath(projectDir, sourceFile);

            // Remove duplicates... keep this new one.
            var previous = _content.FindIndex(e => string.Equals(e.SourceFile, sourceFile, StringComparison.InvariantCultureIgnoreCase));
            if (previous != -1)
                _content.RemoveAt(previous);          

            // Create the item for processing later.
            var item = new ContentItem
            {
                SourceFile = sourceFile,
                ImporterName = Importer,
                ProcessorName = Processor,
                ProcessorParams = new OpaqueDataDictionary()
            };
            _content.Add(item);

            // Copy the current processor parameters blind as we
            // will validate and remove invalid parameters during
            // the build process later.
            foreach (var pair in _processorParams)
                item.ProcessorParams.Add(pair.Key, pair.Value);
        }

        #endregion

        #region IPipelineItem

        public string Name
        {
            get
            {
                if (string.IsNullOrEmpty(FilePath))
                    return "";

                return System.IO.Path.GetFileNameWithoutExtension(FilePath);
            }
        }

        public string Location
        {
            get
            {
                if (string.IsNullOrEmpty(FilePath))
                    return "";

                var idx = FilePath.LastIndexOfAny(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, FilePath.Length - 1);
                return FilePath.Remove(idx);
            }
        }

        [Browsable(false)]
        public string Icon { get; set; }

        #endregion

        public PipelineProject()
        {
            ContentItems = new ReadOnlyCollection<ContentItem>(_content);
            References = new List<string>();
        }

        public void Attach(IProjectObserver observer)
        {            
        }

        public void NewProject()
        {
            Clear();            
        }

        public void OpenProject(string projectFilePath)
        {
            _content.Clear();

            // Store the file name for saving later.
            FilePath = projectFilePath;

            var parser = new CommandLineParser(this);
            parser.Title = "Pipeline";

            var commands = File.ReadAllLines(projectFilePath).
                            Select(x => x.Trim()).
                            Where(x => !string.IsNullOrEmpty(x) && !x.StartsWith("#")).
                            ToArray();

            parser.ParseCommandLine(commands);           

            // Clear variables which store temporary data for the last content item.
            Importer = null;
            Processor = null;
            _processorParams.Clear();
        }

        public void SaveProject()
        {
            const string lineFormat = "/{0}:{1}";
            const string processorParamFormat = "{0}={1}";
            const string commentFormat = "\n#---------------------- {0} ----------------------#\n";
            string line;
            var parameterLines = new List<string>();

            using (var io = File.CreateText(FilePath))
            {
                line = string.Format(commentFormat, "Global Properties");
                io.WriteLine(line);

                line = string.Format(lineFormat, "outputDir", OutputDir);
                io.WriteLine(line);

                line = string.Format(lineFormat, "intermediateDir", IntermediateDir);
                io.WriteLine(line);

                line = string.Format(lineFormat, "platform", Platform);
                io.WriteLine(line);

                line = string.Format(lineFormat, "config", Config);
                io.WriteLine(line);

                line = string.Format(commentFormat, "References");
                io.WriteLine(line);

                foreach (var i in References)
                {
                    line = string.Format(lineFormat, "reference", i);
                    io.WriteLine(line);
                }

                line = string.Format(commentFormat, "Content");
                io.WriteLine(line);

                //string prevProcessor = null;
                foreach (var i in ContentItems)
                {
                    // JCF: Logic for not writting out default values / redundant values is disabled, always write out everything.
                    //if (!i.Importer.FileExtensions.Contains(System.IO.Path.GetExtension(i.SourceFile)))
                    {
                        line = string.Format(lineFormat, "importer", i.ImporterName);
                        io.WriteLine(line);   
                    }

                    // Collect lines for each non-default-value processor parameter
                    // but do not write them yet.
                    parameterLines.Clear();
                    foreach (var j in i.ProcessorParams)
                    {
                        var defaultValue = i.Processor.Properties[j.Key].DefaultValue;
                        if (j.Value == null || j.Value == defaultValue)
                            continue;

                        line = string.Format(lineFormat, "processorParam", string.Format(processorParamFormat, j.Key, j.Value));
                        parameterLines.Add(line);
                    }

                    // If there were any non-default-value processor parameters
                    // or, if the processor itself is not the default processor for this content's importer
                    // or, the processor for this item is different than the previous item's
                    // then we write out the processor command line and any (non default value) processor parameters.
                    // JCF: Logic for not writting out default values / redundant values is disabled, always write out everything.
                    //if (parameterLines.Count > 0 || !i.Processor.TypeName.Equals(i.Importer.DefaultProcessor) || (prevProcessor != null && prevProcessor != i.Processor.TypeName)
                    {
                        line = string.Format(lineFormat, "processor", i.ProcessorName);
                        io.WriteLine(line);

                        // Store the last processor we emitted, so we can test if it does not match subsequent items.
                        //prevProcessor = i.Processor.TypeName;

                        foreach (var ln in parameterLines)
                            io.WriteLine(ln);
                    }

                    line = string.Format(lineFormat, "build", i.SourceFile);
                    io.WriteLine(line);
                    io.WriteLine();
                }
            }
        }

        public void CloseProject()
        {
            Clear();
        }

        public void ImportProject(string projectFilePath)
        {
            Clear();

            FilePath = projectFilePath.Remove(projectFilePath.LastIndexOf('.')) + ".mgcb";

            using (var io = XmlReader.Create(File.OpenText(projectFilePath)))
            {
                string element = null;

                while (io.Read())
                {
                    if (io.NodeType == XmlNodeType.Element)
                    {
                        if (io.LocalName.Equals("Reference"))
                        {
                            string include, hintPath;
                            ReadReference(io, out include, out hintPath);

                            if (!string.IsNullOrEmpty(hintPath))
                                References.Add(hintPath);
                        }
                        else if (io.LocalName.Equals("Compile"))
                        {
                            string include, name, importer, processor;
                            string[] processorParams;
                            ReadCompile(io, out include, out name, out importer, out processor, out processorParams);
                            
                            Importer = importer;
                            Processor = processor;

                            if (processorParams != null)
                            {
                                foreach (var i in processorParams)
                                    AddProcessorParam(i);
                            }

                            var sourceFilePath = Path.GetDirectoryName(projectFilePath);
                            sourceFilePath += "\\" + include;

                            OnBuild(sourceFilePath);
                        }
                    }                   
                }
            }           
        }

        public void RemoveItem(ContentItem item)
        {
            _content.Remove(item);
        }         

        private void Clear()
        {
            _content.Clear();
            References.Clear();
            OutputDir = null;
            IntermediateDir = null;
            Config = null;
            Importer = null;
            Platform = TargetPlatform.Windows;
            Profile = GraphicsProfile.HiDef;
            Processor = null;
            FilePath = null;            
        }

        private void ReadReference(XmlReader io, out string include, out string hintPath)
        {
            include = io.GetAttribute("Include");
            hintPath = null;

            if (!io.IsEmptyElement)
            {
                var depth = io.Depth;
                for (io.Read(); io.Depth != depth; io.Read())
                {
                    // process sub nodes
                    if (io.IsStartElement("HintPath"))
                    {
                        io.Read();
                        hintPath = io.Value;                        
                    }
                }
            }
        }

        private void ReadCompile(XmlReader io,
                                 out string include,
                                 out string name,
                                 out string importer,
                                 out string processor,
                                 out string[] processorParams)
        {
            name = null;
            importer = null;
            processor = null;

            include = io.GetAttribute("Include");
            var parameters = new List<string>();

            if (!io.IsEmptyElement)
            {
                var depth = io.Depth;
                for (io.Read(); io.Depth != depth; io.Read())
                {
                    // process sub nodes here.

                    if (io.IsStartElement())
                    {
                        switch (io.LocalName)
                        {
                            case "Name":                        
                                io.Read();
                                name = io.Value;
                                break;                        
                            case "Importer":
                                io.Read();
                                importer = io.Value;
                                break;
                            case "Processor":
                                io.Read();
                                processor = io.Value;
                                break;      
                            default:
                                if (io.LocalName.Contains("ProcessorParameters_"))
                                {
                                    var line = io.LocalName.Replace("ProcessorParameters_", "");
                                    line += "=";
                                    io.Read();
                                    line += io.Value;
                                    parameters.Add(line);
                                }
                                break;
                        }
                    }
                }            
            }

            processorParams = parameters.ToArray();
        }
    }
}