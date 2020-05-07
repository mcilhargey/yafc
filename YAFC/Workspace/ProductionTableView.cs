using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using YAFC.Model;
using YAFC.Parser;
using YAFC.UI;
using YAFC.UI.Table;

namespace YAFC
{
    public class ProductionTableView : ProjectPageView
    {
        private DataColumn<RecipeRow>[] columns;
        private readonly DataGrid<RecipeRow> grid;
        
        private readonly List<GroupLink> desiredProductsList = new List<GroupLink>();
        private readonly List<GroupLink> linkedProductsList = new List<GroupLink>();
        
        private readonly VirtualScrollList<GroupLink> desiredProducts;
        private readonly VirtualScrollList<GroupLink> linkedProducts;
        private ProductionTable group;
        
        public ProductionTableView()
        {
            columns = new[]
            {
                new DataColumn<RecipeRow>("", BuildRecipePad, 3f),
                new DataColumn<RecipeRow>("Recipe", BuildRecipeName, 15f),
                new DataColumn<RecipeRow>("Entity", BuildRecipeEntity, 7f), 
                new DataColumn<RecipeRow>("Ingredients", BuildRecipeIngredients, 20f),
                new DataColumn<RecipeRow>("Products", BuildRecipeProducts, 20f),
            };
            grid = new DataGrid<RecipeRow>(columns, null);
            desiredProducts = new VirtualScrollList<GroupLink>(7, new Vector2(3, 5f), DrawDesiredProduct, 1) { spacing = 0.2f };
            linkedProducts = new VirtualScrollList<GroupLink>(7, new Vector2(3, 5f), DrawLinkedProduct, 1) { spacing = 0.2f };
        }

        public override void SetModel(ProjectPageContents model)
        {
            if (group != null)
            {
                group.metaInfoChanged -= RefreshHeader;
                group.recipesChanged -= RefreshBody;
            }
            group = model as ProductionTable;
            if (group != null)
            {
                group.metaInfoChanged += RefreshHeader;
                group.recipesChanged += RefreshBody;
                RefreshHeader();
            }
        }

        public override void CreateModelDropdown(ImGui gui, Type type, Project project, ref bool close)
        {
            if (gui.BuildButton("Create production sheet"))
            {
                close = true;
                ProjectPageSettingsPanel.Show(null, (name, icon) =>
                {
                    var page = new ProjectPage(project, type) {icon = icon, name = name};
                    MainScreen.Instance.AddProjectPageAndSetActive(page);
                });
            }
        }

        private void RefreshHeader()
        {
            desiredProductsList.Clear();
            linkedProductsList.Clear();
            foreach (var link in @group.links)
            {
                if (link.amount == 0f)
                    linkedProductsList.Add(link);
                else desiredProductsList.Add(link);
            }
            desiredProductsList.Add(null);
            desiredProducts.data = desiredProductsList;
            linkedProducts.data = linkedProductsList;
            headerContent?.Rebuild();
            bodyContent?.Rebuild();
        }

        private void RefreshBody()
        {
            bodyContent?.Rebuild();
        }

        private void AddRecipe(Recipe recipe)
        {
            var recipeRow = new RecipeRow(group, recipe);
            group.RecordUndo().recipes.Add(recipeRow);
            recipeRow.entity = recipe.crafters.AutoSelect(DataUtils.FavouriteCrafter);
            recipeRow.fuel = recipeRow.entity.energy.fuels.AutoSelect(DataUtils.FavouriteFuel);
        }
        
        private enum ProductDropdownType
        {
            DesiredProduct,
            LinkedProduct,
            Ingredient,
            Product,
            Fuel
        }

        private void CreateLink(Goods goods)
        {
            var existing = group.GetLink(goods);
            if (existing != null)
                return;
            var link = new GroupLink(@group, goods);
            RefreshBody();
            group.RecordUndo().links.Add(link);
        }

        private void DestroyLink(Goods goods)
        {
            var existing = group.GetLink(goods);
            if (existing != null)
            {
                group.RecordUndo().links.Remove(existing);
                RefreshBody();
            }
        }

        private void OpenProductDropdown(ImGui targetGui, Rect rect, Goods goods, ProductDropdownType type, RecipeRow recipe)
        {
            var link = group.GetLink(goods);
            var comparer = DataUtils.GetRecipeComparerFor(goods);
            Action<Recipe> addRecipe = rec =>
            {
                CreateLink(goods);
                AddRecipe(rec);
            };
            var selectFuel = type != ProductDropdownType.Fuel ? null : (Action<Goods>)(fuel =>
            {
                recipe.RecordUndo().fuel = fuel;
                DataUtils.FavouriteFuel.AddToFavourite(fuel);
            });
            MainScreen.Instance.ShowDropDown(targetGui, rect, DropDownContent);

            void DropDownContent(ImGui gui, ref bool close)
            {
                if (type == ProductDropdownType.Fuel && (recipe.entity.energy.fuels.Count > 1 || recipe.entity.energy.fuels[0] != recipe.fuel))
                {
                    close |= gui.BuildInlineObejctListAndButton(recipe.entity.energy.fuels, DataUtils.FavouriteFuel, selectFuel, "Select fuel");
                }
                
                if (goods.production.Length > 0)
                {
                    close |= gui.BuildInlineObejctListAndButton(goods.production, comparer, addRecipe, "Add production recipe");
                }

                if (goods.usages.Length > 0 && gui.BuildButton("Add consumption recipe"))
                {
                    SelectObjectPanel.Select(goods.usages, "Select consumption recipe", addRecipe);
                    close = true;
                }

                if (link != null)
                {
                    if (link.amount != 0)
                        gui.BuildText(goods.locName + " is a desired product and cannot be unlinked.", wrap:true);
                    else gui.BuildText(goods.locName+" production is currently linked. This means that YAFC will try to match production with consumption.", wrap:true);
                    if (type == ProductDropdownType.DesiredProduct)
                    {
                        if (gui.BuildButton("Remove desired product"))
                            link.RecordUndo().amount = 0;
                        if (gui.BuildButton("Remove and unlink"))
                            DestroyLink(goods);
                    } else if (link.amount == 0 && gui.BuildButton("Unlink"))
                    {
                        DestroyLink(goods);
                        close = true;
                    }
                }
                else
                {
                    gui.BuildText(goods.locName+" production is currently NOT linked. This means that YAFC will make no attempt to match production with consumption.", wrap:true);
                    if (gui.BuildButton("Create link"))
                    {
                        CreateLink(goods);
                        close = true;
                    }
                }
            }
        }

        private void OpenObjectSelectDropdown<T>(ImGui targetGui, Rect rect, IReadOnlyList<T> list, IComparer<T> ordering, string header, Action<T> select) where T:FactorioObject
        {
            MainScreen.Instance.ShowDropDown(targetGui, rect, DropDownContent);

            void DropDownContent(ImGui gui, ref bool close)
            {
                close = gui.BuildInlineObejctListAndButton(list, ordering, select, header);
            }
        }

        private void DrawLinkedProduct(ImGui gui, GroupLink element, int index)
        {
            BuildGoodsIcon(gui, element.goods, element.amount, ProductDropdownType.LinkedProduct, null);
        }

        private void DrawDesiredProduct(ImGui gui, GroupLink element, int index)
        {
            gui.allocator = RectAllocator.Stretch;
            gui.spacing = 0f;
            if (element == null)
            {
                if (gui.BuildButton(Icon.Plus, SchemeColor.Primary, SchemeColor.PrimalyAlt, size:3f))
                {
                    SelectObjectPanel.Select(Database.allGoods, "Add desired product", product =>
                    {
                        var existing = @group.GetLink(product);
                        if (existing != null && existing.amount != 0)
                            return;
                        else if (existing != null)
                            existing.RecordUndo().amount = 1f;
                        else group.RecordUndo().links.Add(new GroupLink(@group, product) {amount = 1f});
                    });
                }
            }
            else
            {
                var evt = gui.BuildGoodsWithEditableAmount(element.goods, element.amount, out var newAmount, SchemeColor.Primary);
                if (evt == GoodsWithAmountEvent.ButtonClick)
                    OpenProductDropdown(gui, gui.lastRect, element.goods, ProductDropdownType.DesiredProduct, null);
                else if (evt == GoodsWithAmountEvent.TextEditing && newAmount != 0)
                    element.RecordUndo().amount = newAmount;
            }
        }

        public override void Rebuild(bool visuaOnly = false)
        {
            RefreshHeader();
            base.Rebuild(visuaOnly);
        }

        private void BuildRecipeEntity(ImGui gui, RecipeRow recipe, int index)
        {
            if (gui.BuildObjectWithAmount(recipe.entity, recipe.recipesPerSecond * recipe.recipeTime) && recipe.recipe.crafters.Count > 0)
            {
                OpenObjectSelectDropdown(gui, gui.lastRect, recipe.recipe.crafters, DataUtils.FavouriteCrafter, "Select crafting entity", sel =>
                {
                    DataUtils.FavouriteCrafter.AddToFavourite(sel);
                    if (recipe.entity == sel)
                        return;
                    recipe.RecordUndo().entity = sel;
                    if (!recipe.entity.energy.fuels.Contains(recipe.fuel))
                        recipe.fuel = recipe.entity.energy.fuels.AutoSelect(DataUtils.FavouriteFuel);
                });
            }

            gui.AllocateSpacing(0.5f);
            BuildGoodsIcon(gui, recipe.fuel, recipe.fuelUsagePerSecond * recipe.recipesPerSecond * recipe.recipeTime, ProductDropdownType.Fuel, recipe, true);
        }

        private void BuildGoodsIcon(ImGui gui, Goods goods, float amount, ProductDropdownType dropdownType, RecipeRow recipe, bool isPowerDefault = false)
        {
            var linked = group.GetLink(goods) != null;
            if (gui.BuildObjectWithAmount(goods, amount, linked ? SchemeColor.Primary : SchemeColor.None, goods?.isPower ?? isPowerDefault) && goods != Database.voidEnergy)
            {
                OpenProductDropdown(gui, gui.lastRect, goods, dropdownType, recipe);
            }
        }

        private void BuildRecipeProducts(ImGui gui, RecipeRow recipe, int index)
        {
            foreach (var product in recipe.recipe.products)
                BuildGoodsIcon(gui, product.goods, product.average * recipe.recipesPerSecond * recipe.productionMultiplier, ProductDropdownType.Product, recipe);
        }

        private void BuildRecipeIngredients(ImGui gui, RecipeRow recipe, int index)
        {
            foreach (var ingredient in recipe.recipe.ingredients)
                BuildGoodsIcon(gui, ingredient.goods, ingredient.amount * recipe.recipesPerSecond, ProductDropdownType.Ingredient, recipe);
        }

        private void BuildRecipeName(ImGui gui, RecipeRow recipe, int index)
        {
            gui.spacing = 0.5f;
            if (gui.BuildFactorioObjectButton(recipe.recipe, 3f))
            {
                MainScreen.Instance.ShowDropDown(gui, gui.lastRect, delegate(ImGui imgui, ref bool closed)
                {
                    if (imgui.BuildButton("Delete recipe"))
                    {
                        group.RecordUndo().recipes.Remove(recipe);
                        closed = true;
                    }
                });
            }
            gui.BuildText(recipe.recipe.locName, wrap:true);
        }

        public override void BuildHeader(ImGui gui)
        {
            using (gui.EnterRow())
            {
                using (gui.EnterFixedPositioning(20f, 0f, new Padding(1f)))
                {
                    gui.BuildText("Desired products");
                    desiredProducts.Build(gui);
                }

                using (gui.EnterFixedPositioning(20f, 0f, new Padding(1f)))
                {
                    gui.BuildText("Linked materials");
                    linkedProducts.Build(gui);
                }
            }
            grid.BuildHeader(gui);
        }

        private static readonly Dictionary<WarningFlags, string> WarningsMeaning = new Dictionary<WarningFlags, string>
        {
            {WarningFlags.UnfeasibleCandidate, "Unable to find solution, it may be impossible. This is one of the candidates that may make solution impossible"},
            {WarningFlags.EntityNotSpecified, "Crafter not specified. Solution is inaccurate." },
            {WarningFlags.FuelNotSpecified, "Fuel not specified. Solution is inaccurate." },
            {WarningFlags.LinkedConsumptionNotProduced, "Linked consumption not produced (link ignored)"},
            {WarningFlags.LinkedProductionNotConsumed, "Linked production not consumed (link ignored)"},
            {WarningFlags.FuelWithTemperatureNotLinked, "This recipe uses fuel with temperature. Should link with producing entity to determine temperature."},
            {WarningFlags.TemperatureForIngredientNotMatch, "This recipe does care about ingridient temperature, and the temperature range does not match"},
            {WarningFlags.TemperatureRangeForBoilerNotImplemented, "Boiler have linked different inputs with different temperatures. Reasonong about resulting temperature is not implemented, using minimal temperature instead"},
            {WarningFlags.TemperatureRangeForFuelNotImplemented, "Fuel have linked with production with different temperatures.  Reasonong about resulting temperature is not implemented, using minimal temperature instead"}
        };
        
        private void BuildRecipePad(ImGui gui, RecipeRow row, int index)
        {
            if (row.warningFlags != 0)
            {
                if (gui.BuildRedButton(Icon.Error) == ImGuiUtils.Event.MouseOver)
                {
                    MainScreen.Instance.ShowTooltip(gui, gui.lastRect, g =>
                    {
                        g.boxColor = SchemeColor.Error;
                        g.textColor = SchemeColor.ErrorText;
                        foreach (var (flag, text) in WarningsMeaning)
                        {
                            if ((row.warningFlags & flag) != 0)
                                g.BuildText(text, wrap:true);
                        }
                    });
                }
            }
            else
                gui.BuildText((index+1).ToString());
        }

        public override void BuildContent(ImGui gui)
        {
            var rect = grid.BuildContent(gui, group.recipes);
            if (gui.isBuilding)
                gui.DrawRectangle(rect, SchemeColor.PureBackground);
            if (gui.BuildButton("Add recipe"))
                    SelectObjectPanel.Select(Database.allRecipes, "Add new recipe", AddRecipe);
        }
    }
}