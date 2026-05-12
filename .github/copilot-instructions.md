# Coding Standards & Project Rules

## Project Philosophy
- **SOLID Principles:** Strict adherence to SOLID.
- **Simplicity:** Prefer clear, readable code over clever one-liners.
- **Extraction:** If a method does more than one thing or is too long, extract it.

## C# / WPF Conventions
- **File Structure:** 
  - Fields / Properties at the top.
  - Constructors next.
  - Publics first. Follow Step-down Rule: order methods by appearance in their callers.
  - Private Methods (Logic) in the middle.
  - Event Handlers (Click, etc.) ALWAYS at the bottom of the file.
- **Naming:** Methods must be self-explanatory. If a name needs a comment to be understood, the name is wrong.
- **Comments:** 
  - NO one-liner comments describing what the code does (e.g., `// Increment i`).
  - ONLY use comments for "Why" (reasoning/intent), not "What".
- **Refactoring:** Extract complex logic (e.g., Thumbnailing, COM operations) into dedicated service classes.

## UI/XAML
- Keep `MainWindow.xaml.cs` as thin as possible. 
- Business logic belongs in Services or ViewModels.