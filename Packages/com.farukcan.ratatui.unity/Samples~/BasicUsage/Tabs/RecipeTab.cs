using System.Text;
using UnityEngine;
using RatatuiUnity;

namespace RatatuiUnity.Demo
{
    /// <summary>
    /// Demo2 "Recipe" tab — ingredient list with scrollbar, step-by-step instructions.
    /// </summary>
    public class RecipeTab : ITab
    {
        public string Title => "Recipe";

        private static readonly string[] Ingredients =
        {
            "2 cups flour", "1 tsp baking powder", "1/2 tsp salt", "1/2 cup butter",
            "1 cup sugar", "2 eggs", "1 tsp vanilla", "1/2 cup milk",
            "1 cup chocolate chips", "Pinch of love",
        };

        private static readonly string[] Steps =
        {
            "1. Preheat oven to 375°F (190°C).",
            "2. Mix dry ingredients (flour, baking powder, salt).",
            "3. Cream butter and sugar until fluffy.",
            "4. Beat in eggs and vanilla.",
            "5. Gradually blend in dry ingredients.",
            "6. Stir in milk and chocolate chips.",
            "7. Drop rounded tablespoons onto baking sheets.",
            "8. Bake for 9–11 minutes until golden.",
        };

        private readonly StatefulList<string> _ingredients = new StatefulList<string>(Ingredients);

        public void Update(float dt) { }

        public void OnInput(KeyCode key)
        {
            if (key == KeyCode.UpArrow || key == KeyCode.W)   _ingredients.Previous();
            if (key == KeyCode.DownArrow || key == KeyCode.S) _ingredients.Next();
        }

        public void OnKeyEvent(TerminalKeyEvent e)
        {
            if (e.Key == KeyCode.UpArrow   || e.Character == 'w' || e.Character == 'W') _ingredients.Previous();
            if (e.Key == KeyCode.DownArrow || e.Character == 's' || e.Character == 'S') _ingredients.Next();
        }

        public void OnMouseEvent(TerminalMouseEvent e) { }

        public void Render(RatatuiTerminal term, uint area)
        {
            var cols = term.Split(area, Direction.Horizontal,
                Constraint.Percentage(40),
                Constraint.Percentage(60));

            if (cols.Length < 2) return;

            RenderIngredients(term, cols[0]);
            RenderSteps(term, cols[1]);
        }

        private void RenderIngredients(RatatuiTerminal term, uint area)
        {
            term.Block(area, "Ingredients", Borders.All);
            uint inner = term.Inner(area);

            // List with selection
            term.SetStyle(Color.cyan, Color.clear);
            term.List(inner, _ingredients.ToItemsString(s => s), _ingredients.Selected);

            // Scrollbar on the right edge
            term.Scrollbar(area, Ingredients.Length, System.Math.Max(0, _ingredients.Selected),
                viewportLength: 8, orientation: ScrollbarOrientation.VerticalRight);
        }

        private void RenderSteps(RatatuiTerminal term, uint area)
        {
            term.Block(area, "Instructions", Borders.All);
            uint inner = term.Inner(area);

            var b = term.BeginStyledParagraph(inner, Alignment.Left, true);
            foreach (string step in Steps)
            {
                b.Span(step.Substring(0, 3), fg: Color.yellow, modifiers: Modifier.Bold)
                 .Span(step.Substring(3))
                 .Line();
            }
            b.Render();
        }
    }
}
