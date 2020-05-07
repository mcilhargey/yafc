using System.Diagnostics;
using SDL2;
using YAFC.UI;

namespace YAFC
{
    public class AboutScreen : WindowUtility
    {
        public AboutScreen(Window parent) : base(ImGuiUtils.DefaultScreenPadding)
        {
            Create("About YAFC", 50, parent);
        }
        
        protected override void BuildContents(ImGui gui)
        {
            gui.allocator = RectAllocator.Center;
            gui.BuildText("Yet Another Factorio Calculator", Font.header, align:RectAlignment.Middle);
            gui.BuildText("Copyright 2020 ShadowTheAge", align:RectAlignment.Middle);
            gui.allocator = RectAllocator.LeftAlign;
            gui.AllocateSpacing(1.5f);
            gui.BuildText("This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.", wrap:true);
            gui.BuildText("This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.", wrap:true);
            using (gui.EnterRow(0.3f))
            {
                gui.BuildText("Full license text:");
                BuildLink(gui, "https://gnu.org/licenses/gpl-3.0.html");
            }
            gui.AllocateSpacing(1.5f);
            gui.BuildText("Free and open-source third-party libraries used:", Font.subheader);
            BuildLink(gui, "https://dotnet.microsoft.com/","Microsoft .NET core and libraries");
            using (gui.EnterRow(0.3f))
            {
                BuildLink(gui, "https://libsdl.org/index.php", "Simple DirectMedia Layer 2.0");
                gui.BuildText("and");
                BuildLink(gui, "https://github.com/flibitijibibo/SDL2-CS", "SDL2-CS");
            }
            using (gui.EnterRow(0.3f))
            {
                gui.BuildText("Libraries for SDL2:");
                BuildLink(gui, "http://libpng.org/pub/png/libpng.html", "libpng,");
                BuildLink(gui, "http://libjpeg.sourceforge.net/", "libjpeg,");
                BuildLink(gui, "https://freetype.org", "libfreetype");
                gui.BuildText("and");
                BuildLink(gui, "https://zlib.net/", "zlib");
            }
            using (gui.EnterRow(0.3f))
            {
                gui.BuildText("Google");
                BuildLink(gui, "https://developers.google.com/optimization","OR-Tools,");
                BuildLink(gui, "https://fonts.google.com/specimen/Roboto","Roboto font family");
                gui.BuildText("and");
                BuildLink(gui, "https://material.io/resources/icons", "Material Design Icon collection");
            }

            using (gui.EnterRow(0.3f))
            {
                BuildLink(gui, "https://lua.org/", "Lua 5.3");
                gui.BuildText("and bindings:");
                BuildLink(gui, "https://github.com/NLua/NLua", "NLua");
                gui.BuildText("and");
                BuildLink(gui, "https://github.com/NLua/KeraLua", "KeraLua");
            }

            using (gui.EnterRow(0.3f))
            {
                BuildLink(gui, "https://wiki.factorio.com/", "Documentation on Factorio Wiki");
                gui.BuildText("and");
                BuildLink(gui, "https://lua-api.factorio.com/latest/", "Factorio API reference");
            }
            
            gui.AllocateSpacing(1.5f);
            gui.allocator = RectAllocator.Center;
            gui.BuildText("Factorio name, content and materials are trademarks and copyrights of Wube Software");
            BuildLink(gui, "https://factorio.com/");
        }

        private void BuildLink(ImGui gui, string url, string text = null)
        {
            gui.BuildText(text ?? url, color:SchemeColor.Link);
            var rect = gui.lastRect;
            switch (gui.action)
            {
                case ImGuiAction.MouseMove:
                    gui.ConsumeMouseOver(rect, RenderingUtils.cursorHand);
                    break;
                case ImGuiAction.MouseDown:
                    if (gui.actionParameter == SDL.SDL_BUTTON_LEFT)
                        gui.ConsumeMouseDown(rect);
                    break;
                case ImGuiAction.MouseUp:
                    if (gui.ConsumeMouseUp(rect))
                        Process.Start(new ProcessStartInfo(url) {UseShellExecute = true}); 
                    break;
                case ImGuiAction.Build:
                    if (gui.IsMouseOver(rect))
                        gui.DrawRectangle(new Rect(rect.X, rect.Bottom-0.2f, rect.Width, 0.1f), SchemeColor.Link);
                    break;
            }
        }
    }
}