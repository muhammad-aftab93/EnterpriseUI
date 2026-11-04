using Microsoft.AspNetCore.Razor.TagHelpers;
using EnterpriseUI.Theming;

namespace EnterpriseUI.AspNetCore.TagHelpers;

[HtmlTargetElement("eui-theme-provider")]
public class ThemeProviderTagHelper : TagHelper
{
    public string Theme { get; set; } = "Fluent-Light";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.Attributes.SetAttribute("data-eui-theme", Theme.ToLowerInvariant());
        output.Attributes.SetAttribute("class", $"eui-theme-{Theme.ToLowerInvariant()}");
    }
}

