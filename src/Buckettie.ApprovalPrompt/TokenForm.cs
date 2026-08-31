using System.Globalization;

namespace Buckettie.ApprovalPrompt;

/// <summary>Repository登録用Tokenを入力する最前面Dialogです。</summary>
internal sealed partial class TokenForm : Form
{
    /// <summary>Token入力Dialogを初期化します。</summary>
    internal TokenForm(string repository, string remoteUrl, string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        InitializeComponent();

        bool japanese = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase)
            || string.Equals(language, "auto", StringComparison.OrdinalIgnoreCase)
            && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja";
        Text = japanese ? "Buckettie Repository登録" : "Buckettie Repository Registration";
        instructionLabel.Text = japanese
            ? $"Repository「{repository}」のAPI Tokenを入力してください。"
            : $"Enter the API Token for repository '{repository}'.";
        sourceProjectLabel.Text = japanese ? "登録元プロジェクト名" : "Source project";
        sourceProjectTextBox.Text = repository;
        targetUrlLabel.Text = japanese ? "登録対象リポジトリURL" : "Target repository URL";
        targetUrlTextBox.Text = remoteUrl;
        tokenLabel.Text = japanese ? "Token" : "Token";
        okButton.Text = japanese ? "OK" : "OK";
        cancelButton.Text = japanese ? "キャンセル" : "Cancel";
    }

    /// <summary>入力されたTokenです。</summary>
    internal string Token => tokenTextBox.Text;
}
