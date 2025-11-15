using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace AdvancedBIMLog
{
    [Transaction(TransactionMode.Manual)]
    public class Test : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Get the current Revit application and document
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Get current selection or prompt user to select objects
                Selection selection = uiDoc.Selection;
                ICollection<ElementId> selectedIds = selection.GetElementIds();

                // If no objects are pre-selected, prompt user to select two objects
                if (selectedIds.Count != 2)
                {
                    selectedIds = SelectTwoElements(uiDoc);
                    if (selectedIds == null || selectedIds.Count != 2)
                    {
                        message = "Please select exactly two elements to join.";
                        return Result.Cancelled;
                    }
                }

                // Get the selected elements
                List<Element> selectedElements = selectedIds.Select(id => doc.GetElement(id)).ToList();
                Element element1 = selectedElements[0];
                Element element2 = selectedElements[1];

                // Validate that both elements can be joined
                if (!CanElementsBeJoined(element1, element2))
                {
                    message = $"The selected elements cannot be joined. " +
                             $"Element 1: {element1.Category?.Name ?? "Unknown"}, " +
                             $"Element 2: {element2.Category?.Name ?? "Unknown"}";
                    return Result.Failed;
                }

                // Start a transaction to modify the document
                using (Transaction trans = new Transaction(doc, "Join Selected Objects"))
                {
                    trans.Start();

                    try
                    {
                        // Check if elements are already joined
                        if (JoinGeometryUtils.AreElementsJoined(doc, element1, element2))
                        {
                            TaskDialog.Show("Join Objects",
                                "The selected elements are already joined.");
                            trans.RollBack();
                            return Result.Succeeded;
                        }

                        // Perform the join operation
                        JoinGeometryUtils.JoinGeometry(doc, element1, element2);

                        trans.Commit();

                        // Show success message
                        TaskDialog.Show("Join Objects",
                            $"Successfully joined:\n" +
                            $"• {GetElementDescription(element1)}\n" +
                            $"• {GetElementDescription(element2)}");

                        return Result.Succeeded;
                    }
                    catch (Exception joinEx)
                    {
                        trans.RollBack();
                        message = $"Failed to join elements: {joinEx.Message}";
                        return Result.Failed;
                    }
                }
            }
            catch (Exception ex)
            {
                message = $"An error occurred: {ex.Message}";
                return Result.Failed;
            }
        }

        /// <summary>
        /// Prompts the user to select exactly two elements
        /// </summary>
        private ICollection<ElementId> SelectTwoElements(UIDocument uiDoc)
        {
            List<ElementId> selectedIds = new List<ElementId>();

            try
            {
                // Create a selection filter for joinable elements
                JoinableElementFilter filter = new JoinableElementFilter();

                // Select first element
                TaskDialog.Show("Select Elements", "Select the first element to join.");
                Reference ref1 = uiDoc.Selection.PickObject(ObjectType.Element, filter,
                    "Select the first element to join");
                selectedIds.Add(ref1.ElementId);

                // Select second element
                TaskDialog.Show("Select Elements", "Now select the second element to join.");
                Reference ref2 = uiDoc.Selection.PickObject(ObjectType.Element, filter,
                    "Select the second element to join");
                selectedIds.Add(ref2.ElementId);

                return selectedIds;
            }
            catch 
            {
                return null;
            }
        }

        /// <summary>
        /// Checks if two elements can be joined
        /// </summary>
        private bool CanElementsBeJoined(Element element1, Element element2)
        {
            if (element1 == null || element2 == null)
                return false;

            // Elements must be different
            if (element1.Id == element2.Id)
                return false;

            // Check if elements have 3D geometry
            return HasJoinableGeometry(element1) && HasJoinableGeometry(element2);
        }

        /// <summary>
        /// Checks if an element has geometry that can be joined
        /// </summary>
        private bool HasJoinableGeometry(Element element)
        {
            // Common joinable categories
            var joinableCategories = new[]
            {
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_Roofs,
                BuiltInCategory.OST_Ceilings,
                BuiltInCategory.OST_Columns,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_GenericModel
            };

            if (element.Category != null)
            {
                BuiltInCategory categoryId = (BuiltInCategory)element.Category.Id.IntegerValue;
                return joinableCategories.Contains(categoryId);
            }

            return false;
        }

        /// <summary>
        /// Gets a descriptive string for an element
        /// </summary>
        private string GetElementDescription(Element element)
        {
            string category = element.Category?.Name ?? "Unknown";
            string name = element.Name ?? "Unnamed";
            return $"{category}: {name} (ID: {element.Id})";
        }
    }

    /// <summary>
    /// Selection filter for elements that can be joined
    /// </summary>
    public class JoinableElementFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem == null || elem.Category == null)
                return false;

            // Allow common joinable categories
            var joinableCategories = new[]
            {
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_Roofs,
                BuiltInCategory.OST_Ceilings,
                BuiltInCategory.OST_Columns,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_GenericModel
            };

            BuiltInCategory categoryId = (BuiltInCategory)elem.Category.Id.IntegerValue;
            return joinableCategories.Contains(categoryId);
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false; // We only want to select elements, not references
        }
    }
}

// Additional utility class for batch joining operations
namespace JoinSelectedObjects
{
    [Transaction(TransactionMode.Manual)]
    public class BatchJoinCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Get current selection
                Selection selection = uiDoc.Selection;
                ICollection<ElementId> selectedIds = selection.GetElementIds();

                if (selectedIds.Count < 2)
                {
                    message = "Please select at least two elements to perform batch joining.";
                    return Result.Failed;
                }

                List<Element> selectedElements = selectedIds.Select(id => doc.GetElement(id)).ToList();
                int joinedCount = 0;
                int failedCount = 0;
                List<string> failedPairs = new List<string>();

                using (Transaction trans = new Transaction(doc, "Batch Join Elements"))
                {
                    trans.Start();

                    // Try to join each element with every other element
                    for (int i = 0; i < selectedElements.Count; i++)
                    {
                        for (int j = i + 1; j < selectedElements.Count; j++)
                        {
                            Element elem1 = selectedElements[i];
                            Element elem2 = selectedElements[j];

                            try
                            {
                                if (!JoinGeometryUtils.AreElementsJoined(doc, elem1, elem2))
                                {
                                    JoinGeometryUtils.JoinGeometry(doc, elem1, elem2);
                                    joinedCount++;
                                }
                            }
                            catch
                            {
                                failedCount++;
                                failedPairs.Add($"{elem1.Id} & {elem2.Id}");
                            }
                        }
                    }

                    trans.Commit();
                }

                // Show results
                string resultMessage = $"Batch Join Results:\n" +
                                     $"• Successfully joined: {joinedCount} pairs\n" +
                                     $"• Failed to join: {failedCount} pairs";

                if (failedPairs.Count > 0 && failedPairs.Count <= 10)
                {
                    resultMessage += $"\n\nFailed pairs (Element IDs):\n" +
                                   string.Join("\n", failedPairs);
                }

                TaskDialog.Show("Batch Join Complete", resultMessage);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"An error occurred during batch joining: {ex.Message}";
                return Result.Failed;
            }
        }
    }
}