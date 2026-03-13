using Luban.Defs;
using Luban.RawDefs;
using Luban.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ExcelDataReader;
using NLog;

namespace Luban.Schema.Builtin;

[TableImporter("dxx")]
public class DxxTableImporter : ITableImporter
{
    private static readonly NLog.Logger s_logger = NLog.LogManager.GetCurrentClassLogger();
    
    /// <summary>
    /// 把文件名为 Item.xlsx Sheet 为 Item 和 Drop 的表, 导出为 item_item 和 item_drop 两个类
    /// </summary>
    /// <returns></returns>
    public List<RawTable> LoadImportTables()
    {
        // 自动解析的文件后缀
        HashSet<string> parseSuffix = ["xlsx"];
        
        // 解析增量表
        EnvManager.Current.TryGetOption(null, "diff", false, out var diffFiles);
        var diffList = diffFiles == null ? new List<string>() : diffFiles.Split('|').ToList();
        
        var tables = new List<RawTable>();
        
        string dataDir = GenerationContext.GlobalConf.InputDataDir;             // 策划表文件目录
        
        foreach (string file in Directory.GetFiles(dataDir, "*", SearchOption.AllDirectories))
        {
            if (FileUtil.IsIgnoreFile(dataDir, file))
            {
                continue;
            }
            
            var fileName = Path.GetFileName(file);                      // Archive.xlsx
            var xlsxName = Path.GetFileNameWithoutExtension(fileName);  // Archive
            var suffix = Path.GetExtension(fileName).TrimStart('.');    // xlsx
            // 只导入xlsx文件
            if (!parseSuffix.Contains(suffix))
            {
                continue;
            }
            
            foreach (string sheetName in GetExcelSheetNames(file))
            {
                var name = $"Tb{xlsxName}_{sheetName}"; // 添加Tb前缀，与DefaultTableImporter保持一致
                
                var rawTable = new RawTable()
                {
                    Namespace = string.Empty,
                    Index = string.Empty,
                    Name = name,
                    ValueType = TypeUtil.MakeFullName("", $"{xlsxName}_{sheetName}"),
                    ReadSchemaFromFile = true,
                    Mode = TableMode.MAP,
                    Comment = $"Generated from {fileName} sheet {sheetName}",
                    Groups = [],
                    InputFiles = [$"{sheetName}@{fileName}"], // 指定具体的Sheet
                    OutputFile = string.Empty,
                };
                s_logger.Debug("import dxx table file: {@}", rawTable);
                tables.Add(rawTable);
                
                if (diffList.Contains(name.ToLower()))
                {
                    s_logger.Info("diff table detected, adding both original and diff tables: {name}", name);

                    // 标记原表不导出数据，只生成代码
                    rawTable.ExportData = false;
                    
                    var diffTable = new RawTable()
                    {
                        Namespace = string.Empty,
                        Index = string.Empty,
                        Name = $"{name}_diff",
                        ValueType = rawTable.ValueType,    // 复用原表格的ValueType，这样就不需要生成新的bean代码
                        ReadSchemaFromFile = false,     // 不从文件读取schema，使用原表格的
                        Mode = TableMode.MAP,
                        Groups = [],
                        InputFiles = [$"{sheetName}@{fileName}"], // 指定具体的Sheet
                        OutputFile = string.Empty,
                        GenerateCode = false,           // diff表格不生成任何代码
                        ExportData = true,              // diff表格只导出数据
                    };

                    s_logger.Debug("import dxx diff table: {diffTable}", diffTable);
                    tables.Add(diffTable);
                }
            }
        }
        return tables;
    }
    
    /// <summary>
    /// 获取Excel文件中的所有Sheet名称
    /// 使用现有工程的ExcelReaderFactory来真正读取Excel文件的sheet名称
    /// </summary>
    /// <param name="filePath">Excel文件路径</param>
    /// <returns>Sheet名称列表</returns>
    private List<string> GetExcelSheetNames(string filePath)
    {
        var sheetNames = new List<string>();
        
        try
        {
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                // 使用现有工程的ExcelReaderFactory读取Excel文件
                using (var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream))
                {
                    do
                    {
                        // 收集所有sheet名称
                        if (!string.IsNullOrEmpty(reader.Name) && !reader.Name.StartsWith("#") && !reader.Name.StartsWith("废弃"))
                        {
                            sheetNames.Add(reader.Name);
                        }
                    } while (reader.NextResult());
                }
            }
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "Failed to read sheet names from file: {filePath}", filePath);
            // 读取失败直接返回空列表，不生成假数据
            return new List<string>();
        }
        
        return sheetNames;
    }
}

[TableImporter("default")]
public class DefaultTableImporter : ITableImporter
{
    private static readonly NLog.Logger s_logger = NLog.LogManager.GetCurrentClassLogger();

    public List<RawTable> LoadImportTables()
    {
        string dataDir = GenerationContext.GlobalConf.InputDataDir;

        string fileNamePatternStr = EnvManager.Current.GetOptionOrDefault("tableImporter", "filePattern", false, "#(.*)");
        string tableNamespaceFormatStr = EnvManager.Current.GetOptionOrDefault("tableImporter", "tableNamespaceFormat", false, "{0}");
        string tableNameFormatStr = EnvManager.Current.GetOptionOrDefault("tableImporter", "tableNameFormat", false, "Tb{0}");
        string valueTypeNameFormatStr = EnvManager.Current.GetOptionOrDefault("tableImporter", "valueTypeNameFormat", false, "{0}");
        var fileNamePattern = new Regex(fileNamePatternStr);
        var excelExts = new HashSet<string> { "xlsx", "xls", "xlsm", "csv" };

        var tables = new List<RawTable>();
        foreach (string file in Directory.GetFiles(dataDir, "*", SearchOption.AllDirectories))
        {
            if (FileUtil.IsIgnoreFile(dataDir, file))
            {
                continue;
            }
            string fileName = Path.GetFileName(file);
            string ext = Path.GetExtension(fileName).TrimStart('.');
            if (!excelExts.Contains(ext))
            {
                continue;
            }
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var match = fileNamePattern.Match(fileNameWithoutExt);
            if (!match.Success || match.Groups.Count <= 1)
            {
                continue;
            }

            string relativePath = file.Substring(dataDir.Length + 1).TrimStart('\\').TrimStart('/');
            string namespaceFromRelativePath = Path.GetDirectoryName(relativePath).Replace('/', '.').Replace('\\', '.');

            string rawTableFullName = match.Groups[1].Value;
            string rawTableNamespace = TypeUtil.GetNamespace(rawTableFullName);
            string rawTableName = TypeUtil.GetName(rawTableFullName);
            string tableNamespace = TypeUtil.MakeFullName(namespaceFromRelativePath, string.Format(tableNamespaceFormatStr, rawTableNamespace));
            string tableName = string.Format(tableNameFormatStr, rawTableName);
            string valueTypeFullName = TypeUtil.MakeFullName(tableNamespace, string.Format(valueTypeNameFormatStr, rawTableName));

            var table = new RawTable()
            {
                Namespace = tableNamespace,
                Name = tableName,
                Index = "",
                ValueType = valueTypeFullName,
                ReadSchemaFromFile = true,
                Mode = TableMode.MAP,
                Comment = "",
                Groups = new List<string> { },
                InputFiles = new List<string> { relativePath },
                OutputFile = "",
            };
            s_logger.Debug("import table file:{@}", table);
            tables.Add(table);
        }


        return tables;
    }
}
