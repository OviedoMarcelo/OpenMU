// <copyright file="BossEncounterMap.razor.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Shared.Components.Form;

using System.ComponentModel;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.Shared.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Shows the places of appearance of a boss encounter and the objects of its phases on the map,
/// and moves the selected one to the clicked coordinates.
/// </summary>
public sealed partial class BossEncounterMap : IDisposable
{
    /// <summary>
    /// The size of the map in pixels. A map has 256 x 256 coordinates.
    /// </summary>
    private const int MapSize = 512;

    private const float Scale = MapSize / 256f;

    private const string SpawnPointColor = "#ff3b3b";

    /// <summary>
    /// The colors of the objects of the phases, in the order of the phases.
    /// </summary>
    private static readonly string[] PhaseColors = ["#ffd54a", "#4fc3f7", "#ba68c8", "#ff8a65", "#aed581", "#f06292"];

    private readonly Dictionary<short, string> _images = new();
    private IReadOnlyList<GameMapDefinition> _maps = [];
    private short _mapNumber;
    private string? _imageData;
    private object? _selected;
    private bool _isDragging;

    /// <summary>
    /// Gets or sets the encounter.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public BossEncounterDefinition Encounter { get; set; } = null!;

    [Inject]
    private IDataSource<GameConfiguration> GameConfigurationSource { get; set; } = null!;

    [Inject]
    private IChangeNotificationService NotificationService { get; set; } = null!;

    private IEnumerable<BossEncounterPoint> SpawnPointsOnMap => this.Encounter.SpawnPoints.Where(p => p.MapNumber == this._mapNumber && !p.IsRandomPoint());

    private IReadOnlyList<Marker> Markers
    {
        get
        {
            var markers = this.SpawnPointsOnMap.Select(p => new Marker(p, p.X, p.Y, SpawnPointColor, true)).ToList();
            var phaseIndex = 0;
            foreach (var phase in this.Encounter.GetOrderedPhases())
            {
                var color = PhaseColors[phaseIndex % PhaseColors.Length];
                markers.AddRange(phase.Objects.Select(o => new Marker(o, o.X, o.Y, color, false)));
                phaseIndex++;
            }

            return markers;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.NotificationService.PropertyChanged -= this.OnPropertyChanged;
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync().ConfigureAwait(true);
        this.NotificationService.PropertyChanged += this.OnPropertyChanged;
        await this.GameConfigurationSource.GetOwnerAsync().ConfigureAwait(true);
        this._maps = this.GameConfigurationSource.GetAll<GameMapDefinition>().OrderBy(m => m.Number).ToList();
        this.ShowMap(this.Encounter.SpawnPoints.FirstOrDefault()?.MapNumber ?? this._maps.FirstOrDefault()?.Number ?? 0);
    }

    /// <summary>
    /// Gets the coordinates of the game at a position on the map.
    /// </summary>
    /// <remarks>
    /// Like on the other maps of the admin panel, the x coordinate of the game goes down and the y coordinate to the right.
    /// </remarks>
    private static (byte X, byte Y) GetCoordinates(MouseEventArgs args)
    {
        var x = (byte)Math.Clamp((int)(args.OffsetY / Scale), 0, byte.MaxValue);
        var y = (byte)Math.Clamp((int)(args.OffsetX / Scale), 0, byte.MaxValue);
        return (x, y);
    }

    private void ShowMap(short mapNumber)
    {
        this._mapNumber = mapNumber;
        if (!this._images.TryGetValue(mapNumber, out var imageData)
            && this._maps.FirstOrDefault(m => m.Number == mapNumber) is { } map)
        {
            using var image = new GameMapTerrain(map).ToImage();
            imageData = image.ToBase64String(PngFormat.Instance);
            this._images[mapNumber] = imageData;
        }

        this._imageData = imageData;
    }

    private void OnMapSelected(ChangeEventArgs args)
    {
        if (short.TryParse(args.Value as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mapNumber))
        {
            this.ShowMap(mapNumber);
        }
    }

    private void Select(object item)
    {
        this._selected = item;
    }

    private string GetHint()
    {
        return this._selected is null
            ? "Arrastrá un punto, o elegilo en la lista y hacé clic en el mapa donde va."
            : $"{this.GetLabel(this._selected)}: arrastralo, hacé clic en el mapa donde va, o tocá Supr para borrarlo.";
    }

    private void OnKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Delete")
        {
            this.RemoveSelected();
        }
    }

    /// <summary>
    /// Removes the selected place of appearance, or the selected object from its phase.
    /// </summary>
    private void RemoveSelected()
    {
        switch (this._selected)
        {
            case BossEncounterPoint point when this.Encounter.SpawnPoints.Remove(point):
                this.NotificationService.NotifyChange(this.Encounter, nameof(BossEncounterDefinition.SpawnPoints));
                break;
            case BossEncounterObjectSpawn spawn when this.Encounter.Phases.FirstOrDefault(p => p.Objects.Contains(spawn)) is { } phase:
                phase.Objects.Remove(spawn);
                this.NotificationService.NotifyChange(phase, nameof(BossEncounterPhase.Objects));
                break;
            default:
                // Nothing is selected, or it's already removed.
                return;
        }

        this._selected = null;
    }

    /// <summary>
    /// Moves the selected place or object to the clicked coordinates.
    /// </summary>
    private void OnMapClick(MouseEventArgs args)
    {
        var (x, y) = GetCoordinates(args);
        if (this.MoveSelected(x, y))
        {
            this.NotifySelectedMoved();
        }
    }

    private void StartDrag(object item)
    {
        this._selected = item;
        this._isDragging = true;
    }

    private void OnDragMove(PointerEventArgs args)
    {
        if (!this._isDragging)
        {
            return;
        }

        // The fields of the lists are only updated when the point is dropped, so that they don't flicker.
        var (x, y) = GetCoordinates(args);
        this.MoveSelected(x, y);
    }

    private void OnDragEnd(PointerEventArgs args)
    {
        if (!this._isDragging)
        {
            return;
        }

        this._isDragging = false;
        var (x, y) = GetCoordinates(args);
        this.MoveSelected(x, y);
        this.NotifySelectedMoved();
    }

    /// <summary>
    /// Moves the selected place or object to the coordinates of the shown map.
    /// </summary>
    /// <returns><c>true</c>, if something is selected; otherwise, <c>false</c>.</returns>
    private bool MoveSelected(byte x, byte y)
    {
        switch (this._selected)
        {
            case BossEncounterPoint point:
                point.MapNumber = this._mapNumber;
                point.X = x;
                point.Y = y;
                return true;
            case BossEncounterObjectSpawn spawn:
                spawn.X = x;
                spawn.Y = y;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Updates the fields of the selected place or object in the lists.
    /// </summary>
    private void NotifySelectedMoved()
    {
        switch (this._selected)
        {
            case BossEncounterPoint point:
                this.NotificationService.NotifyChange(point, nameof(BossEncounterPoint.MapNumber));
                this.NotificationService.NotifyChange(point, nameof(BossEncounterPoint.X));
                this.NotificationService.NotifyChange(point, nameof(BossEncounterPoint.Y));
                break;
            case BossEncounterObjectSpawn spawn:
                this.NotificationService.NotifyChange(spawn, nameof(BossEncounterObjectSpawn.X));
                this.NotificationService.NotifyChange(spawn, nameof(BossEncounterObjectSpawn.Y));
                break;
            default:
                // Nothing is selected.
                break;
        }
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        // The coordinates may have been changed in the fields of the lists.
        if (sender is BossEncounterPoint or BossEncounterObjectSpawn or BossEncounterDefinition)
        {
            _ = this.InvokeAsync(this.StateHasChanged);
        }
    }

    private string GetLabel(object item)
    {
        switch (item)
        {
            case BossEncounterPoint point:
                return $"Aparición {this.Encounter.SpawnPoints.ToList().IndexOf(point) + 1}";
            case BossEncounterObjectSpawn spawn:
                var phase = this.Encounter.Phases.FirstOrDefault(p => p.Objects.Contains(spawn));
                var index = phase?.Objects.ToList().IndexOf(spawn) ?? 0;
                return $"{phase?.Name} {index + 1}";
            default:
                return string.Empty;
        }
    }

    private string GetMarkerCssClass(Marker marker)
    {
        var cssClass = marker.IsSpawnPoint ? "boss-map-marker spawn" : "boss-map-marker";
        return marker.Item == this._selected ? cssClass + " selected" : cssClass;
    }

    private string GetMarkerStyle(Marker marker)
    {
        var inv = CultureInfo.InvariantCulture;
        return $"top: {((marker.X + 0.5f) * Scale).ToString(inv)}px; left: {((marker.Y + 0.5f) * Scale).ToString(inv)}px; background: {marker.Color};";
    }

    private string GetCircleStyle(byte x, byte y, byte radius)
    {
        var inv = CultureInfo.InvariantCulture;
        var size = ((2 * radius) + 1) * Scale;
        return $"top: {((x + 0.5f) * Scale).ToString(inv)}px; left: {((y + 0.5f) * Scale).ToString(inv)}px; width: {size.ToString(inv)}px; height: {size.ToString(inv)}px;";
    }

    /// <summary>
    /// A place or an object, which is shown on the map.
    /// </summary>
    /// <param name="Item">The place or the object.</param>
    /// <param name="X">The x coordinate.</param>
    /// <param name="Y">The y coordinate.</param>
    /// <param name="Color">The color.</param>
    /// <param name="IsSpawnPoint">If set to <c>true</c>, it's a place of appearance of the boss.</param>
    private sealed record Marker(object Item, byte X, byte Y, string Color, bool IsSpawnPoint);
}
