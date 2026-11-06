using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.AI;
using EnterpriseUI.Theming;
using EnterpriseUI.WinForms;

namespace EnterpriseUI.WinFormsDemo.Controls;

public class EnterpriseAICopilotControl : UserControl, IThemeableControl
{
    private readonly EnterpriseAIAssistant _assistant = new();
    private readonly RichTextBox _chatHistory=new RichTextBox();
    private readonly TextBox _inputBox=new TextBox();
    private readonly Button _btnSend=new Button();
    private readonly Button _btnQueryGen=new Button();
    private readonly Button _btnSummarize=new Button();
    private readonly Label _statusLabel=new Label();
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;

    public string? ThemeOverride { get; set; }

    public List<AIChatMessage> History => _assistant.History;

    public IAIServiceProvider? ServiceProvider
    {
        get => _assistant.ServiceProvider;
        set => _assistant.ServiceProvider = value;
    }

    public string CurrentPrompt
    {
        get => _assistant.CurrentPrompt;
        set => _assistant.CurrentPrompt = value;
    }

    public bool IsGenerating => _assistant.IsGenerating;

    public EnterpriseAICopilotControl()
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9.5f);

        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(8)
        };

        var lblHeader = new Label
        {
            Text = "EnterpriseUI AI Copilot & Natural Language Query Assistant",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(8, 10)
        };
        topPanel.Controls.Add(lblHeader);

        _chatHistory.            Dock = DockStyle.Fill;

        _chatHistory.            ReadOnly = true;

        _chatHistory.            BackColor = Color.White;

        _chatHistory.            BorderStyle = BorderStyle.FixedSingle;

        _chatHistory.            Font = new Font("Segoe UI", 9.5f)
;

        var bottomContainer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 110,
            Padding = new Padding(8)
        };

        _inputBox.            Location = new Point(8, 8);

        _inputBox.            Width = 460;

        _inputBox.            Font = new Font("Segoe UI", 10f);

        _inputBox.            PlaceholderText = "Ask AI to query data, filter grid records, generate charts, or summarize..."
;
        _inputBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                _ = SendPromptAsync(_inputBox.Text);
            }
        };

        _btnSend.            Text = "Send Prompt";

        _btnSend.            Location = new Point(480, 6);

        _btnSend.            Size = new Size(110, 30);

        _btnSend.            FlatStyle = FlatStyle.Flat
;
        _btnSend.Click += (_, _) => _ = SendPromptAsync(_inputBox.Text);

        _btnQueryGen.            Text = "Filter High-Risk Orders";

        _btnQueryGen.            Location = new Point(8, 44);

        _btnQueryGen.            Size = new Size(160, 28);

        _btnQueryGen.            FlatStyle = FlatStyle.Flat
;
        _btnQueryGen.Click += (_, _) => _ = SendPromptAsync("Show me pending orders above $2,500 with high shipment priority");

        _btnSummarize.            Text = "Generate BI Summary";

        _btnSummarize.            Location = new Point(176, 44);

        _btnSummarize.            Size = new Size(160, 28);

        _btnSummarize.            FlatStyle = FlatStyle.Flat
;
        _btnSummarize.Click += (_, _) => _ = SendPromptAsync("Summarize quarterly revenue variance across top three regions");

        _statusLabel.            Text = "Provider: Offline Mock AI Engine (Zero network latency, 100% deterministic)";

        _statusLabel.            Location = new Point(8, 80);

        _statusLabel.            AutoSize = true;

        _statusLabel.            ForeColor = Color.Gray
;

        bottomContainer.Controls.AddRange(new Control[]
        {
            _inputBox,
            _btnSend,
            _btnQueryGen,
            _btnSummarize,
            _statusLabel
        });

        Controls.Add(_chatHistory);
        Controls.Add(bottomContainer);
        Controls.Add(topPanel);

        // Initial Greeting
        AppendMessage("Enterprise Copilot", "Welcome to the EnterpriseUI Copilot Demonstration. You can interact with simulated intelligent completions, query parsing, and automated BI summarization offline without external API keys.", Color.FromArgb(0, 120, 215));

        ThemeManager.Instance.ThemeChanged += (_, newTheme) => OnThemeChanged(newTheme);
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        EnterpriseThemeEngine.ApplyToControl(this, newTheme);
    }

    public async Task SendPromptAsync(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return;

        AppendMessage("You", prompt, Color.FromArgb(40, 40, 40));
        _inputBox.Text = string.Empty;
        _statusLabel.Text = "AI Engine is synthesizing response...";

        await Task.Delay(350); // Simulate local AI processing

        string reply;
        if (prompt.Contains("order", StringComparison.OrdinalIgnoreCase) || prompt.Contains("filter", StringComparison.OrdinalIgnoreCase))
        {
            reply = "Translated Natural Language to QueryEngine AST:\n" +
                    "• DataSourceRequest.Filters: [Status == 'Pending' AND Total > 2500]\n" +
                    "• SortDescriptors: [Total DESC]\n" +
                    "• Aggregates: [Sum(Total), Count()]\n" +
                    "Execution Result: 14 matches identified. DataGrid view updated automatically.";
        }
        else if (prompt.Contains("summarize", StringComparison.OrdinalIgnoreCase) || prompt.Contains("revenue", StringComparison.OrdinalIgnoreCase))
        {
            reply = "Executive BI Summary:\n" +
                    "• Total quarterly revenue attained $5.28M (+14.2% YoY).\n" +
                    "• Top performing region: Asia-Pacific ($2.10M), driven by cloud infrastructure adoptions.\n" +
                    "• Risk factor: Supply chain lead times extended by 3.4 days in European logistics centers.";
        }
        else
        {
            reply = $"Simulated AI Copilot completed intent for: \"{prompt}\".\nOptimal control recommendation: EnterpriseDataGrid with virtual scrolling and conditional formatting rules.";
        }

        AppendMessage("Enterprise Copilot", reply, Color.FromArgb(0, 120, 215));
        _statusLabel.Text = "Provider: Offline Mock AI Engine (Zero network latency, 100% deterministic)";
    }

    private void AppendMessage(string sender, string text, Color headerColor)
    {
        _chatHistory.SelectionStart = _chatHistory.TextLength;
        _chatHistory.SelectionLength = 0;

        _chatHistory.SelectionColor = headerColor;
        _chatHistory.SelectionFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _chatHistory.AppendText($"[{DateTime.Now:HH:mm:ss}] {sender}:\n");

        _chatHistory.SelectionColor = Color.FromArgb(30, 30, 30);
        _chatHistory.SelectionFont = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        _chatHistory.AppendText($"{text}\n\n");
        _chatHistory.ScrollToCaret();
    }
}

