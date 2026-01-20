/*
DocumentType: WallLengthAudit
Categories: Architectural, QA, Audit
Author: Paracore Team
Dependencies: RevitAPI 2025, CoreScript.Engine, Paracore.Addin

Description:
Audits wall lengths on a specified Level and isolates walls shorter than a user-defined threshold. Outputs a table of results and selects/isolates matching walls in the active view.

UsageExamples:
- "Audit walls shorter than 3000mm on Level 1"
- "Find and isolate short walls on Level 2"
- "List wall lengths on Ground Floor below 2000mm"
*/

// Create Params instance (engine fills UI values into this class)
var p = new Params(); // <- user inputs are available here

// Find the Level element that matches the provided name
Level? level = new FilteredElementCollector(Doc)
    .OfClass(typeof(Level))            // collect Level objects
    .Cast<Level>()                     // cast to typed enumerable
    .FirstOrDefault(l => l.Name == p.TargetLevel); // pick the one with matching name

// If not found, stop with a clear error (engine will show it)
if (level == null) throw new Exception($"🚫 Level not found: '{p.TargetLevel}'");

// Collect all Wall elements that belong to the chosen level
var wallsOnLevel = new FilteredElementCollector(Doc)
    .OfClass(typeof(Wall))             // collect Wall objects
    .Cast<Wall>()                      // cast to Wall type
    .Where(w => w.LevelId == level.Id) // filter by LevelId
    .ToList();                         // materialize list

// Threshold value (already in internal units: feet) from Params
double thresholdFeet = p.MaxLengthThreshold; // engine converted from mm to feet

// Filter the walls to only those shorter than the threshold
var shortWalls = wallsOnLevel
    .Where(w =>                         // for each wall
    {
        var lenParam = w.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH); // wall length param
        if (lenParam == null || !lenParam.HasValue) return false;           // skip if missing
        return lenParam.AsDouble() < thresholdFeet;                        // compare in feet
    })
    .ToList();                           // materialize short walls list

// If none found, print a friendly message (convert threshold back to mm for display)
if (!shortWalls.Any())
{
    double thresholdMm = UnitUtils.ConvertFromInternalUnits(thresholdFeet, UnitTypeId.Millimeters); // convert to mm
    Println($"✅ No walls shorter than {Math.Round(thresholdMm, 2)} mm on {p.TargetLevel}.");
}
else
{
    // Summary print
    Println($"📊 Found {shortWalls.Count} short walls on {p.TargetLevel}.");

    // Prepare table rows: id, name, type name, length in mm
    var rows = shortWalls.Select(w =>
    {
        var lenFt = w.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble(); // length in feet
        var lenMm = Math.Round(UnitUtils.ConvertFromInternalUnits(lenFt, UnitTypeId.Millimeters), 2); // convert to mm
        var wallType = Doc.GetElement(w.GetTypeId()) as WallType; // get WallType element

        return new
        {
            Id = w.Id.Value,                 // numeric id
            Name = w.Name,                   // element name
            Type = wallType?.Name ?? "Unknown", // type name or Unknown
            Length_mm = lenMm                // length rounded in mm
        };
    }).ToList();

    // Show the results in a table for users to inspect
    Table(rows);

    // Select the short walls in the UI and isolate them in the active view
    var idsToSelect = shortWalls.Select(w => w.Id).ToList();
    UIDoc.Selection.SetElementIds(idsToSelect); // set selection (no transaction needed)

    // View isolation is a change to the view: run a single named transaction
    Transact("Isolate Short Walls", () =>
    {
        Doc.ActiveView.IsolateElementsTemporary(idsToSelect);
    });

    Println($"🔍 {shortWalls.Count} walls selected and isolated in the active view.");
}

// ----------------------
// Parameters class (engine requires this at the bottom)
// ----------------------
public class Params
{

    #region Audit Settings

    /// <summary>Which Level should we audit?</summary>
    [RevitElements(TargetType = "Level")]
    [Required]
    public string? TargetLevel { get; set; } // level name selected by user

    /// <summary>Maximum length threshold to flag walls (user-entered in mm).</summary>
    [Range(0, 50000, 100)]
    [Unit("mm")] // engine converts this value to internal units (feet) before script runs
    public double MaxLengthThreshold { get; set; } = 3000; // default 3000 mm

    #endregion

}