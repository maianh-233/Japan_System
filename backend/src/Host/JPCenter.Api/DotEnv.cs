namespace JPCenter.Api;

/// <summary>
/// Đọc file <c>.env</c> (KEY=VALUE mỗi dòng) vào biến môi trường của tiến trình — chỉ để chạy local cho tiện.
/// Biến môi trường đã có sẵn được giữ nguyên (ưu tiên hơn file). Không thêm thư viện cho việc vài dòng code.
/// </summary>
internal static class DotEnv
{
    /// <summary>Tìm <c>.env</c> từ thư mục hiện tại đi ngược lên (tối đa 6 cấp) — chạy từ Host hay từ backend/ đều được.</summary>
    public static void Load()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var i = 0; dir is not null && i < 6; i++, dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, ".env");
            if (!File.Exists(file)) continue;

            foreach (var raw in File.ReadAllLines(file))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;

                var key = line[..eq].Trim();
                var value = line[(eq + 1)..].Trim().Trim('"');
                if (Environment.GetEnvironmentVariable(key) is null) Environment.SetEnvironmentVariable(key, value);
            }
            return;
        }
    }
}
