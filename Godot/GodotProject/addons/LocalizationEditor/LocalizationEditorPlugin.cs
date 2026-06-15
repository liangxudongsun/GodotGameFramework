using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

[Tool]
public partial class LocalizationEditorPlugin : EditorPlugin
{
	const string MENU_ITEM_NAME = "GenerateLocalizationFile";
	const string OUTPUT_PATH = "res://TheGame/DataTables/Localizations/";

	private static readonly XNamespace Ns =
		"http://schemas.openxmlformats.org/spreadsheetml/2006/main";

	private static readonly XNamespace RelNs =
		"http://schemas.openxmlformats.org/officeDocument/2006/relationships";

	public override void _EnterTree()
	{
		AddToolMenuItem(MENU_ITEM_NAME, new Callable(this, nameof(OnMenuPressed)));
	}

	private void OnMenuPressed()
	{
		string projectRoot = ProjectSettings.GlobalizePath("res://");
		string sourceDir = Path.GetFullPath(Path.Combine(projectRoot, "../../Configs/Localization/"));
		string outputDir = ProjectSettings.GlobalizePath(OUTPUT_PATH);

		if (!Directory.Exists(sourceDir))
		{
			GD.PrintErr($"未找到源目录: {sourceDir}");
			return;
		}

		Directory.CreateDirectory(outputDir);

		foreach (string excelFile in Directory.GetFiles(sourceDir, "*.xlsx"))
		{
			GD.Print($"处理: {Path.GetFileName(excelFile)}");
			ProcessExcel(excelFile, outputDir);
		}

		EditorInterface.Singleton.GetResourceFilesystem().Scan();
		GD.Print("生成完成。");
	}

	private void ProcessExcel(string excelPath, string outputDir)
	{
		try
		{
			using (ZipArchive archive = ZipFile.OpenRead(excelPath))
			{
				List<string> sharedStrings = ParseSharedStrings(archive);
				Dictionary<string, string> sheets = ParseSheets(archive);

				foreach (var kv in sheets)
				{
					string sheetName = SanitizeName(kv.Key);
					string sheetFile = kv.Value;

					string tsv = ParseSheetToTsv(archive, sheetFile, sharedStrings);
					if (string.IsNullOrEmpty(tsv))
						continue;

					if (File.Exists(Path.Combine(outputDir, $"{sheetName}.txt")))
						File.Delete(Path.Combine(outputDir, $"{sheetName}.txt"));
					File.WriteAllText(Path.Combine(outputDir, $"{sheetName}.txt"), tsv, Encoding.UTF8);
					GD.Print($"  → {sheetName}.txt");
				}
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"  失败: {ex.Message}");
		}
	}

	private List<string> ParseSharedStrings(ZipArchive archive)
	{
		var entry = archive.GetEntry("xl/sharedStrings.xml");
		if (entry == null) return new List<string>();

		using var stream = entry.Open();
		var doc = XDocument.Load(stream);
		return doc.Root.Elements(Ns + "si").Select(si =>
		{
			var t = si.Element(Ns + "t");
			if (t != null) return t.Value;
			return string.Concat(si.Elements(Ns + "r").Elements(Ns + "t").Select(e => e.Value));
		}).ToList();
	}

	private Dictionary<string, string> ParseSheets(ZipArchive archive)
	{
		var result = new Dictionary<string, string>();
		var rels = new Dictionary<string, string>();

		var relEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
		if (relEntry != null)
		{
			using var stream = relEntry.Open();
			var doc = XDocument.Load(stream);
			foreach (var rel in doc.Root.Elements())
			{
				string id = rel.Attribute("Id")?.Value;
				string target = rel.Attribute("Target")?.Value;
				if (id != null && target != null)
					rels[id] = "xl/" + target.Replace('\\', '/');
			}
		}

		var wbEntry = archive.GetEntry("xl/workbook.xml");
		if (wbEntry == null) return result;

		using (var stream = wbEntry.Open())
		{
			var doc = XDocument.Load(stream);
			var sheetsEl = doc.Root?.Element(Ns + "sheets");
			if (sheetsEl == null) return result;

			foreach (var sheet in sheetsEl.Elements(Ns + "sheet"))
			{
				string name = sheet.Attribute("name")?.Value;
				string rId = sheet.Attribute(RelNs + "id")?.Value;
				if (name != null && rId != null && rels.TryGetValue(rId, out string path))
					result[name] = path;
			}
		}

		return result;
	}

	private string ParseSheetToTsv(ZipArchive archive, string sheetFile, List<string> sharedStrings)
	{
		var entry = archive.GetEntry(sheetFile);
		if (entry == null) return null;

		using var stream = entry.Open();
		var doc = XDocument.Load(stream);
		var sheetData = doc.Root?.Element(Ns + "sheetData");
		if (sheetData == null) return null;

		var sb = new StringBuilder();

		foreach (var row in sheetData.Elements(Ns + "row"))
		{
			int lastCol = -1;
			foreach (var cell in row.Elements(Ns + "c"))
			{
				string refStr = cell.Attribute("r")?.Value;
				if (string.IsNullOrEmpty(refStr)) continue;

				int colIndex = CellRefToColIndex(refStr);
				if (colIndex > 3) continue; // 只输出 A-D

				// 首个单元格输出 colIndex 个 tab；后续输出 (colIndex - lastCol) 个
				int tabs = lastCol < 0 ? colIndex : colIndex - lastCol;
				for (int i = 0; i < tabs; i++)
					sb.Append('\t');

				sb.Append(GetCellValue(cell, sharedStrings));
				lastCol = colIndex;
			}
			sb.AppendLine();
		}

		return sb.ToString();
	}

	private static int CellRefToColIndex(string cellRef)
	{
		// "B6" -> "B", "AA1" -> "AA"
		int i = 0;
		while (i < cellRef.Length && char.IsLetter(cellRef[i]))
			i++;
		string col = cellRef.Substring(0, i);

		// A=0, B=1, ..., Z=25, AA=26, ...
		int result = 0;
		foreach (char c in col)
			result = result * 26 + (c - 'A' + 1);
		return result - 1;
	}

	private string GetCellValue(XElement cell, List<string> sharedStrings)
	{
		string type = cell.Attribute("t")?.Value ?? "";
		string v = cell.Element(Ns + "v")?.Value ?? "";

		if (type == "s" && int.TryParse(v, out int si) && si >= 0 && si < sharedStrings.Count)
			return sharedStrings[si];

		if (type == "inlineStr")
		{
			var isEl = cell.Element(Ns + "is");
			return isEl?.Element(Ns + "t")?.Value ?? "";
		}

		return v;
	}

	private string SanitizeName(string name)
	{
		char[] invalid = Path.GetInvalidFileNameChars();
		return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
	}

	public override void _ExitTree()
	{
		RemoveToolMenuItem(MENU_ITEM_NAME);
	}
}
