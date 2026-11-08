namespace MdStudio.Models;

/// <summary>Commands of the formatting bar (specification 4.9.1), plus the paragraph styles.</summary>
public enum FormatAction
{
    /// <summary>Undo the last change.</summary>
    Undo,

    /// <summary>Redo the undone change.</summary>
    Redo,

    /// <summary>Turn the current block into a paragraph.</summary>
    Paragraph,

    /// <summary>Turn the current block into a level 1 heading.</summary>
    Heading1,

    /// <summary>Turn the current block into a level 2 heading.</summary>
    Heading2,

    /// <summary>Turn the current block into a level 3 heading.</summary>
    Heading3,

    /// <summary>Toggle bold.</summary>
    Bold,

    /// <summary>Toggle italic.</summary>
    Italic,

    /// <summary>Toggle strikethrough.</summary>
    Strikethrough,

    /// <summary>Wrap the selection in inline code.</summary>
    InlineCode,

    /// <summary>Toggle a bulleted list.</summary>
    BulletList,

    /// <summary>Toggle a numbered list.</summary>
    NumberedList,

    /// <summary>Insert a task list.</summary>
    TaskList,

    /// <summary>Toggle a quote.</summary>
    Quote,

    /// <summary>Insert or apply a link.</summary>
    Link,

    /// <summary>Insert an image.</summary>
    Image,

    /// <summary>Insert a table.</summary>
    Table,

    /// <summary>Insert a code block.</summary>
    CodeBlock,

    /// <summary>Insert a Mermaid diagram.</summary>
    Mermaid,

    /// <summary>Insert a horizontal rule.</summary>
    HorizontalRule,
}
