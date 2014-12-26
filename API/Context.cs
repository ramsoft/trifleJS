﻿using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Noesis.Javascript;

namespace TrifleJS.API
{
    /// <summary>
    /// Core JavaScript engine context
    /// </summary>
    public class Context : JavascriptContext
    {
        /// <summary>
        /// Executes a javascript file
        /// </summary>
        /// <param name="filepath">Path to file, this can either be relative to triflejs.exe or current script being executed.</param>
        /// <returns></returns>
        public object RunFile(string filepath)
        {
            FileInfo file = Find(filepath);
            if (file == null || !file.Exists)
            {
                throw new Exception(String.Format("File does not exist {0}.", filepath));
            }
            // Read file 
            string script = File.ReadAllText(file.FullName);
            // Execute file
            return RunScript(script, file.Name);
        }

        /// <summary>
        /// Executes a script
        /// </summary>
        /// <param name="script">String containing javascript to execute</param>
        /// <param name="scriptName">Name of the script</param>
        public object RunScript(string script, string scriptName)
        {
            // Read file and add a blank function at the end, 
            // this is to fix a stackoverflow bug
            // in Javascript.NET where it tries
            // to return an object with circular reference
            script += ";(function() {})();";
            // Tag which file we are executing
            Phantom.scriptName = scriptName;
            // Execute 
            return base.Run(script, scriptName);
        }

        /// <summary>
        /// Finds a file either in the current working directory or in the LibraryPath.
        /// Returns null if not found.
        /// </summary>
        /// <param name="path">path to find</param>
        /// <returns></returns>
        public FileInfo Find(string path)
        {
            FileInfo file = new FileInfo(path);
            if (!file.Exists)
            {
                string currentDir = Environment.CurrentDirectory;
                Environment.CurrentDirectory = Phantom.libraryPath;
                file = new FileInfo(path);
                Environment.CurrentDirectory = currentDir;
            }
            return file;
        }

        /// <summary>
        /// Gets the current context
        /// </summary>
        public static Context Current
        {
            get { return Program.Context; }
        }

        /// <summary>
        /// Handles an exception
        /// </summary>
        /// <param name="ex"></param>
        public static void Handle(Exception ex)
        {
            List<Dictionary<string, object>> traceData = new List<Dictionary<string, object>>();
            JavascriptException jsEx = ex as JavascriptException;
            string message = ex.Message;
            if (jsEx != null)
            {
                // Remove refs to Host environment & output javascript error
                message = jsEx.Message.Replace("TrifleJS.Host+", "");
                traceData.Add(new Dictionary<string, object> { 
                    {"file", jsEx.Source},
                    {"line", jsEx.Line},
                    {"col", jsEx.StartColumn},
                    {"function", jsEx.TargetSite.Name}
                });
            }
            else
            {
                StackTrace trace = new StackTrace(ex);
                foreach (var frame in trace.GetFrames())
                {
                    traceData.Add(new Dictionary<string, object> { 
                        {"file", frame.GetFileName()},
                        {"line", frame.GetFileLineNumber()},
                        {"col", frame.GetFileColumnNumber()},
                        {"function", frame.GetMethod().Name}
                    });
                }
            }
            var err = traceData[0];
            Console.error(String.Format("{0} ({1},{2}): {3}", err.Get("file"), err.Get("line"), err.Get("col"), message));
        }


        /// <summary>
        /// Parses input/output to make it JavaScript friendly
        /// </summary>
        /// <param name="arguments">an array of argument objects (of any type)</param>
        /// <returns>list of parsed arguments</returns>
        public static string[] Parse(params object[] arguments)
        {
            List<string> input = new List<string>();
            foreach (object argument in arguments)
            {
                input.Add(ParseOne(argument));
            }
            return input.ToArray();
        }

        /// <summary>
        /// Parses input/output tomake it JavaScript friendly
        /// </summary>
        /// <param name="argument">an argument object (of any type)</param>
        /// <returns>the parsed argument</returns>
        public static string ParseOne(object argument)
        {
            if (argument == null)
            {
                return "null";
            }
            else
            {
                switch (argument.GetType().Name)
                {
                    case "Int32":
                    case "Double":
                        return argument.ToString();
                    case "Boolean":
                        return argument.ToString().ToLowerInvariant();
                    case "String":
                        // Fix for undefined (coming up as null)
                        if ("{{undefined}}".Equals(argument))
                        {
                            return "undefined";
                        }
                        else
                        {
                            return String.Format("\"{0}\"", argument.ToString().Replace("\"", "\\\""));
                        }
                    default:
                        return Utils.Serialize(argument);
                }
            }
        }

    }

}
