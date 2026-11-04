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
    private readonly RichTextBox _chatHistory;
    private readonly TextBox _inputBox;
    private readonly Button _btnSend;
    private readonly Button _btnQueryGen;
    private readonly Button _btnSummarize;
    private readonly Label _statusLabel;
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

        _chatHistory = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9.5f)
        };

        var bottomContainer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 110,
            Padding = new Padding(8)
        };

        _inputBox = new TextBox
        {
            Location = new Point(8, 8),
            Width = 460,
            Font = new Font("Segoe UI", 10f),
            PlaceholderText = "Ask AI to query data, filter grid records, generate charts, or summarize..."
        };
        _inputBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                _ = SendPromptAsync(_inputBox.Text);
            }
        };

        _btnSend = new Button
        {
            Text = "Send Prompt",
            Location = new Point(480, 6),
            Size = new Size(110, 30),
            FlatStyle = FlatStyle.Flat
        };
        _btnSend.Click += (_, _) => _ = SendPromptAsync(_inputBox.Text);

        _btnQueryGen = new Button
        {
            Text = "Filter High-Risk Orders",
            Location = new Point(8, 44),
            Size = new Size(160, 28),
            FlatStyle = FlatStyle.Flat
        };
        _btnQueryGen.Click += (_, _) => _ = SendPromptAsync("Show me pending orders above $2,500 with high shipment priority");

        _btnSummarize = new Button
        {
            Text = "Generate BI Summary",
            Location = new Point(176, 44),
            Size = new Size(160, 28),
            FlatStyle = FlatStyle.Flat
        };
        _btnSummarize.Click += (_, _) => _ = SendPromptAsync("Summarize quarterly revenue variance across top three regions");

        _statusLabel = new Label
        {
            Text = "Provider: Offline Mock AI Engine (Zero network latency, 100% deterministic)",
            Location = new Point(8, 80),
            AutoSize = true,
            ForeColor = Color.Gray
        };

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

