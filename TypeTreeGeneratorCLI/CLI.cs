﻿using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using AssetStudio;

namespace TypeTreeGeneratorCLI
{
    public class CLI
    {
        public static Generator.Generator gen = new Generator.Generator();

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine(
"A simple typtree generator based in Perfare's AssetStudio." +
"Argumens:\n" +
"-p  ~ assembly folder path (required)\n" +
"-a  ~ assembly name (required)\n" +
"-c  ~ class name (if not set, all classes will be dumped)\n" +
"-n  ~ namespace (optional for set class name)\n" +
"-d  ~ dump style (simple, json, json_min, bin)\n" +
"-z  ~ if used, the data will be compressed via gzip\n" +
"-o  ~ output path (instead of printing the data will be stored in the given path)\n" +
"All binary formats (bin, compressed) are displayed as base64 string."
                );
                return;
            }
            string assemblyFolder = "";
            string m_AssemblyName = "";
            string m_ClassName = "";
            string m_Namespace = "";
            int[] version = { 0, 0, 0, 0 };
            string dump = "simple";
            bool zip = false;
            string output = "";
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-p":
                        assemblyFolder = args[i + 1];
                        i++;
                        break;
                    case "-a":
                        m_AssemblyName = args[i + 1];
                        i++;
                        break;
                    case "-c":
                        m_ClassName = args[i + 1];
                        i++;
                        break;
                    case "-n":
                        m_Namespace = args[i + 1];
                        i++;
                        break;
                    case "-v":
                        version = args[i + 1].Split(',').Select(int.Parse) as int[];
                        i++;
                        break;
                    case "-d":
                        dump = args[i + 1];
                        i++;
                        break;
                    case "-z":
                        zip = true;
                        break;
                    case "-o":
                        output = args[i + 1];
                        i++;
                        break;
                }
            }

            // load typedefs
            gen.loadFolder(assemblyFolder);
            IEnumerable<TypeDefinition> typeDefs = gen.getTypeDefs(m_AssemblyName, m_ClassName, m_Namespace);
            string str = "";
            byte[] data = new byte[] { };
            switch (dump)
            {
                case "simple":
                    str = simpleDump(typeDefs, version).ToString();
                    break;
                case "json":
                    data = jsonDump(typeDefs, version, true);
                    break;
                case "json_min":
                    data = jsonDump(typeDefs, version, false);
                    break;
                case "bin":
                    data = binDump(typeDefs, version);
                    break;
            }
            Dump(str, data, zip, output);
        }

        public static void Dump(string str, byte[] data, bool zip, string outpath)
        {
            if (outpath.Length > 0)
            {
                if (data.Length == 0)
                {
                    data = Encoding.UTF8.GetBytes(str);
                }
                if (zip)
                {
                    data = Zip(data);
                }
                File.OpenWrite(outpath).Write(data);
            }
            else
            {
                if (zip)
                {
                    if (str.Length > 0)
                    {
                        data = Encoding.UTF8.GetBytes(str);
                    }
                    data = Zip(data);
                }
                if (data.Length > 0)
                {
                    str = Convert.ToBase64String(data);
                }
                Console.WriteLine(str);
            }

        }

        public static StringBuilder simpleDump(IEnumerable<TypeDefinition> typeDefs, int[] version)
        {
            StringBuilder sb = new StringBuilder();
            foreach (TypeDefinition typeDef in typeDefs)
            {
                try
                {
                    List<TypeTreeNode> nodes = gen.convertToTypeTreeNodes(typeDef, version);
                    sb.AppendLine(typeDef.Name);
                    foreach (TypeTreeNode node in nodes)
                    {
                        sb.AppendLine($"{node.level}\t{node.type}\t{node.name}\t{node.meta_flag}");
                    }

                    sb.AppendLine("");
                }
                catch
                {
                    continue;
                }
            }
            return sb;
        }

        public static byte[] jsonDump(IEnumerable<TypeDefinition> typeDefs, int[] version, bool indented)
        {
            SortedDictionary<string, List<TypeTreeNode>> nodeDict = new SortedDictionary<string, List<TypeTreeNode>>();
            foreach (TypeDefinition typeDef in typeDefs)
            {
                String key;
                List<TypeTreeNode> nodes;
                try
                {
                    key = typeDef.Namespace.Length > 0 ? $"{typeDef.Namespace}.{typeDef.Name}" : typeDef.Name;
                    nodes = gen.convertToTypeTreeNodes(typeDef, version);
                    if (nodes.Count > 0) nodeDict.Add(key, nodes);
                }
                catch { continue; }
            }
            var options = new JsonSerializerOptions
            {
                WriteIndented = indented,

            };
            return JsonSerializer.SerializeToUtf8Bytes(nodeDict, options);
        }

        public static void writeString(MemoryStream s, string str)
        {
            byte[] bin = Encoding.UTF8.GetBytes(str);
            s.WriteByte((byte)bin.Length);
            s.Write(bin, (int)s.Length, bin.Length);
        }

        public static byte[] binDump(IEnumerable<TypeDefinition> typeDefs, int[] version)
        {
            MemoryStream s = new MemoryStream();
            foreach (TypeDefinition typeDef in typeDefs)
            {
                String key;
                List<TypeTreeNode> nodes;
                try
                {
                    key = typeDef.Namespace.Length > 0 ? $"{typeDef.Namespace}.{typeDef.Name}" : typeDef.Name;
                    nodes = gen.convertToTypeTreeNodes(typeDef, version);
                    if (nodes.Count == 0) continue;
                    writeString(s, key);
                    s.Write(BitConverter.GetBytes((UInt32)nodes.Count), (int)s.Length, 4);
                    foreach (var node in nodes)
                    {
                        s.Write(BitConverter.GetBytes(node.level), (int)s.Length, 4);
                        writeString(s, node.type);
                        writeString(s, node.name);
                        s.Write(BitConverter.GetBytes(node.meta_flag), (int)s.Length, 4);
                    }
                }
                catch { continue; }
            }
            return s.ToArray();
        }

        public static byte[] Zip(byte[] bytes)
        {
            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(mso, CompressionMode.Compress))
                {
                    msi.CopyTo(gs);
                }

                return mso.ToArray();
            }
        }
    }
}
