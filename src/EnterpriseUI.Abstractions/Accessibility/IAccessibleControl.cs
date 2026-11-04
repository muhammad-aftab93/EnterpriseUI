namespace EnterpriseUI.Abstractions.Accessibility;

public enum AriaRole
{
    None,
    Button,
    Grid,
    Row,
    Cell,
    ColumnHeader,
    RowHeader,
    Tree,
    TreeItem,
    TabList,
    Tab,
    TabPanel,
    Menu,
    MenuItem,
    Dialog,
    AlertDialog,
    ProgressBar,
    Slider,
    SpinButton,
    ComboBox,
    ListBox,
    Option,
    TextBox,
    Switch,
    Toolbar,
    Navigation,
    Region,
    Status,
    Alert
}

public interface IAccessibleControl
{
    AriaRole Role { get; set; }
    string? AriaLabel { get; set; }
    string? AriaDescription { get; set; }
    string? AriaLabelledBy { get; set; }
    string? AriaDescribedBy { get; set; }
    bool AriaHidden { get; set; }
    bool AriaExpanded { get; set; }
    bool AriaDisabled { get; set; }
    bool AriaRequired { get; set; }
    bool AriaInvalid { get; set; }
}

