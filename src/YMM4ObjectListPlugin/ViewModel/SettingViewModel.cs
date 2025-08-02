using System.Diagnostics;
using Epoxy;
using YmmeUtil.Common;
using YmmeUtil.Ymm4;

namespace ObjectList.ViewModel;

[ViewModel]
public class SettingViewModel
{
	public string PluginVersion { get; private set; } = "?";
	public string UpdateMessage { get; set; } =
		"Update checkボタンを押してください";
	public bool HasUpdate { get; set; }
	public bool IsUpdateCheckEnabled { get; set; } = true;
	public bool IsDownloadable { get; set; }

	public static bool IsShowFooter =>
		ObjectListSettings.Default.IsShowFooter;

	public Command UpdateCheck { get; set; }
	public Command Download { get; set; }
	public Command OpenGithub { get; set; }

	readonly UpdateChecker checker;

	const string GithubUrl =
		$"https://github.com/InuInu2022/YMM4ObjectListPlugin";

	public SettingViewModel()
	{
		PluginVersion = AssemblyUtil.GetVersionString(
			typeof(Ymm4ObjectListPlugin)
		);

		checker = UpdateChecker.Build(
			"InuInu2022",
			"YMM4ObjectListPlugin"
		);

		UpdateCheck = Command.Factory.Create(async () =>
		{
			IsUpdateCheckEnabled = false;
			TaskbarUtil.StartIndeterminate();

			HasUpdate = await checker
				.IsAvailableAsync(typeof(SettingViewModel))
				.ConfigureAwait(true);
			IsDownloadable = HasUpdate;
			UpdateMessage = await GetUpdateMessageAsync()
				.ConfigureAwait(true);

			IsUpdateCheckEnabled = true;
			TaskbarUtil.FinishIndeterminate();
		});

		Download = Command.Factory.Create(async () =>
		{
			try
			{
				var result = await checker
					.GetDownloadUrlAsync(
						"Ymm4ObjectListPlugin.",
						$"{GithubUrl}/releases"
					)
					.ConfigureAwait(false);
				await OpenUrlAsync(result)
					.ConfigureAwait(false);
			}
			catch (Exception e)
			{
				await Console
					.Error.WriteLineAsync(e.Message)
					.ConfigureAwait(false);
			}
		});

		OpenGithub = Command.Factory.Create(
			async () =>
				await OpenUrlAsync(GithubUrl)
					.ConfigureAwait(false)
		);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Usage",
		"SMA0040:Missing Using Statement",
		Justification = "<保留中>"
	)]
	static async Task<Process> OpenUrlAsync(string openUrl)
	{
		return await Task.Run(() =>
			{
				return Process.Start(
						new ProcessStartInfo()
						{
							FileName = openUrl,
							UseShellExecute = true,
						}
					) ?? new();
			})
			.ConfigureAwait(false);
	}

	async ValueTask<string> GetUpdateMessageAsync()
	{
		return HasUpdate
			? $"プラグインの更新があります {await checker.GetRepositoryVersionAsync().ConfigureAwait(false)}"
			: "プラグインは最新です";
	}
}
