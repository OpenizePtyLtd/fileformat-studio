# 📁 FileFormat Studio

> **Chat with your documents locally & natively on Windows.**  
> An open-source WinUI 3 desktop application that turns your Word, Excel, PowerPoint, and PDF files into an interactive AI knowledgebase.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform: WinUI 3](https://img.shields.io/badge/Platform-WinUI%203%20%7C%20.NET%208+-0078D7.svg)]()
[![Ecosystem](https://img.shields.io/badge/Ecosystem-FileFormat.ai-5856D6)](https://fileformat.ai)

---

## ✨ Key Highlights

- 💬 **Chat with Documents:** Build custom local knowledgebases from `.docx`, `.xlsx`, `.pptx`, `.pdf`, and more.
- 🤖 **Multi-LLM Integrations:** Connect seamlessly with OpenAI, Anthropic Claude, Google Gemini, Azure OpenAI, or local models via Ollama.
- 🔌 **Pluggable Document Engine:** 
  - **100% Free & Open-Source:** Built-in community/OSS file parsers with zero licensing fees.
  - **Enterprise Fidelity (Optional):** Plug in your own [Aspose](https://www.aspose.com/) license for advanced rendering and high-fidelity parsing.
- ⚡ **Native Windows Experience:** Fast, responsive WinUI 3 interface with dark/light mode and modern fluent design.

---

## 📥 Installation & Releases

Pre-compiled production releases are published on the [GitHub Releases](https://github.com/OpenizePtyLtd/fileformat-studio/releases) page.

1. **Standard Installer (Recommended):**
   - Download `FileFormatAIStudio-Setup.exe` from the latest release.
   - Run the installer wizard to set up shortcuts and install to Program Files.
2. **Portable ZIP:**
   - Download `FileFormatAIStudio-Portable-win-x64.zip`.
   - Extract anywhere and run `FileFormatAIStudio.exe` directly (no installation or admin rights required).

---

## 🚀 Release Management (Maintainers)

The repository includes an automated GitHub Actions release workflow (`.github/workflows/release.yml`) and Inno Setup configuration (`installer.iss`).

### Publishing a Monthly Release
1. Create and push a version tag matching your monthly release cadence (e.g., `v0.10.0`):
   ```bash
   git tag v0.10.0
   git push origin v0.10.0
   ```
   *(Or trigger the **Build & Publish Release** workflow manually from the **Actions** tab in GitHub).*
2. The workflow will:
   - Compile the self-contained WinUI 3 desktop application (`win-x64`).
   - Package `NodeHost` parsers and runtime dependencies.
   - Generate `FileFormatAIStudio-Setup.exe` via Inno Setup and `FileFormatAIStudio-Portable-win-x64.zip`.
   - Open a **Draft Release** on GitHub populated with categorized release notes.
3. Review the release notes under [GitHub Releases](https://github.com/OpenizePtyLtd/fileformat-studio/releases) and click **Publish release**.