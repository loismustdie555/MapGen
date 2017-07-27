﻿using Delaunay.Geo;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGen : MonoBehaviour {

    public MapSettings mapSettings;

    private enum GenerationState { Waiting, RoomsSeperated,}
    private GenerationState currentState;

    private List<MapRoom> mapRooms;

    // Use this for initialization
    void Start () {
        currentState = GenerationState.Waiting;
	}
	
	// Update is called once per frame
	void Update () {
    }

    private void GenerateMap(List<MapRoom> rooms)
    {
        SnapRoomLocationToGrid(this.transform);

        List<MapRoom> hubRooms = GetHubRooms(rooms, mapSettings.hubRoomCutoff);

        // Re-generate if not enough hub rooms are found
        if (hubRooms.Count <= mapSettings.minAmountOfHubRooms)
        {
            throw new NotImplementedException();
            currentState = GenerationState.Reset;
            return;
        }

        List<Vector2> hubRoomCenterPoints = new List<Vector2>();
        foreach (MapRoom room in hubRooms)
        {
            hubRoomCenterPoints.Add(room.centerPoint);
        }

        List<LineSegment> connectingLineSegments = DelaunayGrapher.FindConnectingLineSegments(hubRoomCenterPoints, mapSettings.percentOfRoomConnectionAboveMinPath);

        List<Line> hallwayLines = CreateHallwayLinesFromSegments(connectingLineSegments, hubRooms);

        List<MapRoom> hallwayRooms = FindHallwayRoomsBetweenHubRooms(hallwayLines, hubRooms);

        Point bottomLeftPoint = FindBottomLeftPointInMap(hubRooms, hallwayRooms, hallwayLines);
        Point upperRightPoint = FindUpperRightPointInMap(hubRooms, hallwayRooms, hallwayLines);

        hubRooms.OffsetMaproomGridLocationsToNewMapOrigin(bottomLeftPoint);
        hallwayRooms.OffsetMaproomGridLocationsToNewMapOrigin(bottomLeftPoint);
        hallwayLines = LineTools.OffsetLinesToNewMapOrigin(hallwayLines, bottomLeftPoint);

        int mapWidth = upperRightPoint.X - bottomLeftPoint.X;
        int mapHeight = upperRightPoint.Y - bottomLeftPoint.Y;
        int[][] map = CreateBaseMap(mapWidth, mapHeight);

        AddRoomsToMap(ref map, hubRooms);
        AddRoomsToMap(ref map, hallwayRooms);
        AddLinesToMap(ref map, hallwayLines);
        List<MapRoom> fillerRooms = MapRoomTools.CreateRoomsFromFiller(map);
    }

    /// <summary>
    /// Modifies the basic map array with filler ids (1) in line positions.
    /// </summary>
    /// <param name="map">Basic map array</param>
    /// <param name="lines">Lines that will be added to map as filler</param>
    private void AddLinesToMap(ref int[][] map, List<Line> lines)
    {
        foreach (Line line in lines) {
            Point p0 = new Point((int)line.p0.x, (int)line.p0.y);
            Point p1 = new Point((int)line.p1.x, (int)line.p1.y);

            // Determine if the width and height steps will go in negitive directions.
            int negX = p0.X < p1.X ? 1 : -1;
            int negY = p0.Y < p1.Y ? 1 : -1;

            int absWidth = Mathf.Abs(p0.X - p1.X);
            int absHeight = Mathf.Abs(p0.Y - p1.Y);

            // Set the map area to 1, which is the 'filler' UID. 
            for (int x = 0; x <= absWidth; x++) {
                // Get the width position.
                int xPos = p0.X + (x * negX);
                for (int y = 0; y <= absHeight; y++) {
                    // Get the height position
                    int yPos = p0.Y + (y * negY);

                    // If the map position is not already assigned, then add 'filler' for hallway. Set to 1. 
                    if (map[xPos][yPos] == 0)
                        map[xPos][yPos] = 1;
                }
            }
        }
    }

    /// <summary>
    /// Modfies the basic map array with room ids in their positions.
    /// </summary>
    /// <param name="map">Basic map array</param>
    /// <param name="rooms">Rooms to add to map</param>
    private void AddRoomsToMap(ref int[][] map, List<MapRoom> rooms)
    {
        foreach (MapRoom room in rooms) {
            int locX = room.gridLocation.X;
            int locY = room.gridLocation.Y;
            for (int x = 0; x < room.width; x++) {
                for (int y = 0; y < room.height; y++) {
                    map[locX + x][locY + y] = room.Id;
                }
            }
        }
    }

    /// <summary>
    /// Create and zero out a 2d array for the basic map info.
    /// </summary>
    /// <param name="mapWidth">Width of the used map area.</param>
    /// <param name="mapHeight">Height of the used map area.</param>
    /// <returns></returns>
    private int[][] CreateBaseMap(int mapWidth, int mapHeight)
    {
        int[][] map = new int[mapWidth][];

        // Zero map out
        for (int mapX = 0; mapX < map.Length; mapX++) {
            map[mapX] = new int[mapHeight];
            for (int mapY = 0; mapY < map[mapX].Length; mapY++) {
                map[mapX][mapY] = 0;
            }
        }

        return map;
    }

    private Point FindUpperRightPointInMap(List<MapRoom> hubRooms, List<MapRoom> hallwayRooms, List<Line> hallwayLines)
    {
        List<Point> points = new List<Point>()
        {
            MapRoomTools.FindGreatestPointInRooms(hubRooms),
            MapRoomTools.FindGreatestPointInRooms(hallwayRooms),
            MapRoomTools.FindGreatestPointInHallways(hallwayLines),
        };

        int greatestX = 0;
        int greatestY = 0;
        for (int i = 0; i < points.Count; i++)
        {
            if (i == 0)
            {
                greatestX = points[i].X;
                greatestY = points[i].Y;
            }
            else
            {
                if (greatestX < points[i].X)
                    greatestX = points[i].X;
                if (greatestY < points[i].Y)
                    greatestY = points[i].Y;
            }
        }

        return new Point(greatestX, greatestY);
    }

    private Point FindBottomLeftPointInMap(List<MapRoom> hubRooms, List<MapRoom> hallwayRooms, List<Line> hallwayLines)
    {
        List<Point> points = new List<Point>()
        {
            MapRoomTools.FindLowestPointInRooms(hubRooms),
            MapRoomTools.FindLowestPointInRooms(hallwayRooms),
            MapRoomTools.FindLowestPointInHallways(hallwayLines),
        };

        int lowestX = 0;
        int lowestY = 0;
        for (int i = 0; i < points.Count; i++)
        {
            if (i == 0)
            {
                lowestX = points[i].X;
                lowestY = points[i].Y;
            }
            else
            {
                if (lowestX > points[i].X)
                    lowestX = points[i].X;
                if (lowestY > points[i].Y)
                    lowestY = points[i].Y;
            }
        }

        return new Point(lowestX, lowestY);
    }

    /// <summary>
    /// Use hallway lines to create raycasts that will be used to find all unused rooms between hubrooms.
    /// </summary>
    /// <param name="hallwayLines">Lines that run from one hub room to another.</param>
    /// <param name="hubRooms">Main 'hub' rooms already discoverd.</param>
    /// <returns></returns>
    private List<MapRoom> FindHallwayRoomsBetweenHubRooms(List<Line> hallwayLines, List<MapRoom> hubRooms)
    {
        List<MapRoom> foundRooms = new List<MapRoom>();

        foreach (Line line in hallwayLines)
        {
            // Find heading 
            Vector2 rayHeading = line.p0 - line.p1;

            // Find distance
            float rayDistance = rayHeading.magnitude;

            // Find direction
            Vector2 rayDirection = rayHeading / rayDistance;

            // Throw 2d Raycast
            RaycastHit2D[] roomsHit = Physics2D.RaycastAll(line.p1, rayDirection, rayDistance);

            // Check found rooms vs Hub rooms; Add to hallwayRooms if not a hub room.
            for (int i = 0; i < roomsHit.Length; i++)
            {
                MapRoom room = roomsHit[i].transform.GetComponent<MapRoomHolder>().mapRoom;

                if (!hubRooms.Contains(room) && !foundRooms.Contains(room))
                {
                    foundRooms.Add(room);
                }
            }
        }

        return foundRooms;
    }

    /// <summary>
    /// Creates lines between rooms connected by the delaunay voroni graph line segments.
    /// </summary>
    /// <param name="segments">Graph line segments</param>
    /// <param name="rooms">Rooms with found connections in delaunay voroni graphing step</param>
    /// <returns></returns>
    private List<Line> CreateHallwayLinesFromSegments(List<LineSegment> segments, List<MapRoom> rooms)
    {
        List<Line> hallwayLines = new List<Line>();

        int hallwayBuffer = mapSettings.sizeOfHallways / 2;

        // Switch that changes up the direction of hall way drawing.
        // Makes for a better visual on end product.
        bool switchDirectionOfLines = false; 

        foreach (LineSegment segment in segments)
        {
            MapRoom r0;
            MapRoom r1;
            r0 = MapRoomTools.FindRoomContainingPoint(rooms, segment.p0.Value);
            r1 = MapRoomTools.FindRoomContainingPoint(rooms, segment.p1.Value);

            Point midPoint = MapRoomTools.MidPointBetweenMapRooms(r0, r1);

            Vector2 startPoint;
            Vector2 endPoint;

            if (MapRoomTools.IsPointBetweenXBoundariesOfGivenRooms(midPoint, r0, r1, hallwayBuffer)) // Stright hallway
            {
                // Create lines from mid point then up and down to rooms.
                startPoint = new Vector2(midPoint.X, r0.centerPoint.y);
                endPoint = new Vector2(midPoint.X, r1.centerPoint.y);

                hallwayLines.AddRange(CreateHallwayLinesOfSetWidth(startPoint, endPoint, mapSettings.sizeOfHallways, false));
            }
            else if (MapRoomTools.IsPointBetweenYBoundariesOfGivenRooms(midPoint, r0, r1, hallwayBuffer)) // Stright hallway
            {
                // Create lines from mid point then left and right to rooms.
                startPoint = new Vector2(r0.centerPoint.x, midPoint.Y);
                endPoint = new Vector2(r1.centerPoint.x, midPoint.Y);

                hallwayLines.AddRange(CreateHallwayLinesOfSetWidth(startPoint, endPoint, mapSettings.sizeOfHallways, true));
            }
            else // Right angle bend in hallway
            {
                int lineAdjustmentY = r0.centerPoint.y > r1.centerPoint.y ? 1 : -1;
                int lineAdjustmentX = r0.centerPoint.x > r1.centerPoint.x ? 1 : -1;

                if (switchDirectionOfLines)
                {
                    startPoint = new Vector2(r0.centerPoint.x + lineAdjustmentX, r0.centerPoint.y);
                    endPoint = new Vector2(r1.centerPoint.x - lineAdjustmentX, r0.centerPoint.y);
                    hallwayLines.AddRange(CreateHallwayLinesOfSetWidth(startPoint, endPoint, mapSettings.sizeOfHallways, true));

                    startPoint = new Vector2(r1.centerPoint.x, r1.centerPoint.y - lineAdjustmentY);
                    endPoint = new Vector2(r1.centerPoint.x, r0.centerPoint.y + lineAdjustmentY);
                    hallwayLines.AddRange(CreateHallwayLinesOfSetWidth(startPoint, endPoint, mapSettings.sizeOfHallways, false));
                }
                else
                {
                    startPoint = new Vector2(r0.centerPoint.x, r0.centerPoint.y + lineAdjustmentY);
                    endPoint = new Vector2(r0.centerPoint.x, r1.centerPoint.y - lineAdjustmentY);
                    hallwayLines.AddRange(CreateHallwayLinesOfSetWidth(startPoint, endPoint, mapSettings.sizeOfHallways, false));

                    startPoint = new Vector2(r1.centerPoint.x - lineAdjustmentX, r1.centerPoint.y);
                    endPoint = new Vector2(r0.centerPoint.x + lineAdjustmentX, r1.centerPoint.y);
                    hallwayLines.AddRange(CreateHallwayLinesOfSetWidth(startPoint, endPoint, mapSettings.sizeOfHallways, true));
                }

                // Flip the line direciton.
                switchDirectionOfLines = !switchDirectionOfLines;
            }
        }

        return hallwayLines;
    }

    /// <summary>
    /// Gets a set number of hallway line segments beteween two rooms with a set width.
    /// </summary>
    /// <param name="startPoint">Point to start the line at</param>
    /// <param name="endPoint">Point to end the line at</param>
    /// <param name="r0">Room 1</param>
    /// <param name="r1">Room 2</param>
    /// <param name="sizeOfHallways">The size of the hallway to make</param>
    /// <param name="isHorizontal">Is the line horizontal or vertical?</param>
    /// <returns></returns>
    private List<Line> CreateHallwayLinesOfSetWidth(Vector2 startPoint, Vector2 endPoint, int sizeOfHallways, bool isHorizontal) {
        List<Line> segments = new List<Line>();

        // Add base line
        segments.Add(new Line(startPoint, endPoint));

        // Counter for while loop. Tracks the added width to each line.
        // Starts at 1 since the base line is already in.
        int widthAdded = 1;

        // Distance to spread out the width lines, basically unit in engine.
        int distance = 1;

        // Add lines, then add extra lines to create desired width of hallways.
        while (widthAdded < sizeOfHallways) {

            if (isHorizontal) { // Line is horizontal
                // Add line for width to positive Vertical side of line.
                segments.Add(new Line(new Vector2(startPoint.x, startPoint.y + distance),
                                      new Vector2(endPoint.x, endPoint.y + distance)));

                // Step width
                widthAdded++;

                // Add line for width to other side of line
                if (widthAdded < sizeOfHallways) { // recheck size
                    segments.Add(new Line(new Vector2(startPoint.x, startPoint.y - distance),
                                          new Vector2(endPoint.x, endPoint.y - distance)));
                }
            }
            else { // Line is vertical
                // Add line for width to positive Horizontal side of line.
                segments.Add(new Line(new Vector2(startPoint.x + distance, startPoint.y),
                                      new Vector2(endPoint.x + distance, endPoint.y)));

                // Step width
                widthAdded++;

                // Add line for width to other side of line
                if (widthAdded < sizeOfHallways) { // recheck size
                    segments.Add(new Line(new Vector2(startPoint.x - distance, startPoint.y),
                                          new Vector2(endPoint.x - distance, endPoint.y)));
                }
            }

            // Step width.
            widthAdded++;
            // Step distance.
            distance++;
        }

        return segments;
    }

    /// <summary>
    /// Finds and returns all rooms that are large enough to be main or 'hub' rooms.
    /// </summary>
    /// <param name="rooms">Rooms to check for hubs</param>
    /// <param name="cutoff">Adjustment to further limit allowed lower size of hub rooms</param>
    /// <returns></returns>
    private List<MapRoom> GetHubRooms(List<MapRoom> rooms, float cutoff)
    {
        int height_mean = 0;
        int width_mean = 0;

        foreach (MapRoom room in rooms) {
            height_mean += room.height;
            width_mean += room.width;
        }

        height_mean = Mathf.RoundToInt((float)(height_mean / rooms.Count) * cutoff);
        width_mean = Mathf.RoundToInt((float)(width_mean / rooms.Count) * cutoff);

        List<MapRoom> hubRooms = new List<MapRoom>();
        foreach (MapRoom room in rooms) {
            if (room.width > width_mean && room.height > height_mean)
                hubRooms.Add(room);
        }

        return hubRooms;
    }

    /// <summary>
    /// Updates location of all map rooms by their physical helper objects.
    /// </summary>
    /// <param name="roomHolder">Parent transform that holds physical helper objects</param>
    private void SnapRoomLocationToGrid(Transform roomHolder)
    {
        foreach (Transform child in roomHolder)
        {
            Point location = new Point(Mathf.RoundToInt(child.position.x), Mathf.RoundToInt(child.position.y));
            child.GetComponent<MapRoomHolder>().mapRoom.gridLocation = location;
        }
    }

    // Removes and resets all created objects to get ready for clean generation
    private void ResetGeneration()
    {
        mapRooms = new List<MapRoom>();

        RemovePhysicalRoomObjects(this.transform);
    }

    // Removes all physical helper room games objects
    private void RemovePhysicalRoomObjects(Transform roomHolder)
    {
        foreach (Transform room in roomHolder)
        {
            Destroy(room.gameObject);
        }
    }

    // Create the foundation map rooms and physical helper rooms then wait till the phys engine seperates helpers
    public void GenerateRooms()
    {
        mapRooms = GenerateMapRooms();

        GeneratePhysicalRooms(mapRooms);

        StartCoroutine(WaitTillRoomsSeperate(this.transform.parent));
    }

    // Create the physical helper object to utilize the physics engine for room seperation
    private void GeneratePhysicalRooms(List<MapRoom> mapRooms)
    {
        foreach (MapRoom room in mapRooms)
        {
            GameObject physicalRoom = new GameObject("PhysicalRoom", typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(MapRoomHolder));
            physicalRoom.GetComponent<MapRoomHolder>().mapRoom = room;
            physicalRoom.transform.position = new Vector3(room.gridLocation.X, room.gridLocation.Y);
            physicalRoom.transform.localScale = new Vector3(room.width, room.height);
            physicalRoom.transform.SetParent(this.transform.parent, true);
        }
    }

    private List<MapRoom> GenerateMapRooms()
    {
        MapRoomCreator roomCreator = new MapRoomCreator(mapSettings.numberOfRoomsToCreate, 
            mapSettings.roomSpawnEllipsisAreaWidth, mapSettings.roomSpawnEllipsisAreaHeight,
            mapSettings.roomMeanHeight, mapSettings.roomMeanWidth, mapSettings.roomStandardDeviation, 
            mapSettings.roomMaxWidth, mapSettings.roomMinWidth, mapSettings.roomMaxHeight, mapSettings.roomMinHeight);

        List<MapRoom> rooms = roomCreator.CreateRooms();

        return rooms;
    }

    private IEnumerator WaitTillRoomsSeperate(Transform roomHolder)
    {
        bool roomsAsleep;

        float savedTimeScale = Time.timeScale;

        Time.timeScale = mapSettings.speedOfPhysicsSeperation;

        // Check all rooms to see if they have settled into place
        do
        { 
            roomsAsleep = true;

            yield return new WaitForSeconds(1f);

            foreach (Transform trans in roomHolder)
            {
                if (trans.GetComponent<Rigidbody2D>().IsAwake())
                {
                    roomsAsleep = false;
                    break;
                }
            }

        } while (!roomsAsleep);

        Time.timeScale = savedTimeScale;
    }
}
