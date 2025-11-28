using Game.Data;
using System.IO;
using System.Text.RegularExpressions;

public static class ObfuscatorSettingsModifier
{
	static readonly string _obfSettingsPath = ApplicationInfo.ProjectPath + "/Assets/GUPS/Obfuscator/Settings/Obfuscator_Settings.json";
	public static void ToggleObfuscator(bool isEnabled)
	{
		if (!File.Exists(_obfSettingsPath)) return;

		string text = File.ReadAllText(_obfSettingsPath);

		// "Key": "Global_Enable_Obfuscation" 인 객체 내부의 "Value": "True"/"False"만 교체
		string pattern = @"(""Key""\s*:\s*""Global_Enable_Obfuscation""[^}]*?""Value""\s*:\s*"")(true|false|""True""|""False"")";
		string replacement = $"$1{(isEnabled ? "True" : "False")}";

		string newText = Regex.Replace(text, pattern, replacement, RegexOptions.IgnoreCase);

		File.WriteAllText(_obfSettingsPath, newText);
	}

	public static void SetupObfCustomFilePaths(string customLogPath, string customMappingPath)
	{
		string customLogDir = Path.GetDirectoryName(customLogPath);
		string customMappingDir = Path.GetDirectoryName(customMappingPath);
			
		if (!Directory.Exists(customLogDir))
		{
			Directory.CreateDirectory(customLogDir);
		}
			
		if (!Directory.Exists(customMappingDir))
		{
			Directory.CreateDirectory(customMappingDir);
		}
			
		if (!File.Exists(_obfSettingsPath)) return;
			
		//1. Log 파일 경로 지정
		string text = File.ReadAllText(_obfSettingsPath);
		string pattern =
			@"(""Key""\s*:\s*""Custom_Log_FilePath""[^}]*?""Value""\s*:\s*"")([^""]*)("")";

		string replacement = $"$1{customLogPath}$3";
		string newText = Regex.Replace(
			text,
			pattern,
			replacement,
			RegexOptions.IgnoreCase | RegexOptions.Singleline
		);
		
		//2. Mapping 파일 경로 지정
		pattern = 
			@"(""Key""\s*:\s*""Save_Mapping_FilePath""[^}]*?""Value""\s*:\s*"")([^""]*)("")";
		
		replacement = $"$1{customMappingPath}$3";
		
		newText = Regex.Replace(
			newText,
			pattern,
			replacement,
			RegexOptions.IgnoreCase | RegexOptions.Singleline
		);
		
		File.WriteAllText(_obfSettingsPath, newText);
	}
}
