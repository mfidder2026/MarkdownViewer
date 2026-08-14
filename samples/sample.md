# Markdown Viewer — Sample Document

This is a **sample** Markdown file used to smoke-test the viewer. It exercises
most of the supported elements.

## Inline formatting

You can write *italic*, **bold**, ***bold italic***, and ~~strikethrough~~ text.
There is also `inline code` and a [hyperlink to example.com](https://example.com).

## Headings

### H3 heading
#### H4 heading
##### H5 heading
###### H6 heading

## Lists

Unordered:

- First item
- Second item
  - Nested item A
  - Nested item B
- Third item

Ordered:

1. One
2. Two
   1. Two point one
   2. Two point two
3. Three

Task list:

- [x] Done task
- [ ] Not done task

## Blockquote

> This is a blockquote.
> It can span multiple lines.
>
> And contain a nested paragraph.

## Code block

```csharp
public class Example
{
    public void Hello()
    {
        // indentation and line breaks are preserved
        Console.WriteLine("Hello, world!");
    }
}
```

## Horizontal rule

---

## Table

| Feature       | Supported | Notes            |
|---------------|:---------:|------------------|
| Headings      |    yes    | H1–H6            |
| Tables        |    yes    | GitHub-flavoured |
| Code blocks   |    yes    | No highlighting  |
| Images        |    yes    | Local + remote   |

## Image

A relative image reference (the file is not present in this sample, so you
should see a placeholder instead of a crash):

![Architecture](images/architecture.png)