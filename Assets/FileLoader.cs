﻿using UnityEngine;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;

public class FileLoader : MonoBehaviour
{
    // Use this for initialization
    void Start()
    {

        GeometryLoader gl = GameObject.Find("GeometryLoader").GetComponent<GeometryLoader>();
        gl.setTheme(new LabThemingMode());

        var GeometryXML = EditorUtility.OpenFilePanel(
            "Load Geometry", "", "xml");

        var TrajectoryXML = EditorUtility.OpenFilePanel(
            "Load Trajectory", "", "xml");

        loadPedestrianFile(TrajectoryXML);
        loadGeometryFile(GeometryXML);
    }

    // Update is called once per frame
    void Update() { }

    void loadGeometryFile(string filename)
    {

        if (!System.IO.File.Exists(filename))
        {
            Debug.Log("Error: File " + filename + " not found.");
            return;
        }

        XmlDocument xmlDocGeo = new XmlDocument();
        xmlDocGeo.LoadXml(System.IO.File.ReadAllText(filename));

        int wallNr = 1;     //includes "Walls" and "Obsacles"
        int stairNr = 1;    //includes "Stairs" and "Escalators"
        int floorNr = 1;    //floors are generated for each subroom at this state

        XmlNode geometry = xmlDocGeo.SelectSingleNode("//geometry");
        foreach (XmlElement rooms in geometry.SelectNodes("//rooms"))
        {
            foreach (XmlElement room in geometry.SelectNodes("//room"))
            {
                foreach (XmlElement subroom in room.SelectNodes("subroom[@class='subroom']"))
                {   
                    float height = 2.5f;
                    float zOffset = TryParseWithDefault.ToSingle(subroom.GetAttribute("C_z"), 0);       //EXCEPTION: sometimes "C_z" is referred to as "C"

                    if (zOffset == 0)
                    {
                        zOffset = TryParseWithDefault.ToSingle(subroom.GetAttribute("C"), 0);
                    }

                    foreach (XmlElement openWall in subroom.SelectNodes("polygon[@caption='wall']"))
                    {
                        WallExtrudeGeometry.create(openWall.GetAttribute("caption") + (" ") + wallNr, parsePoints(openWall), height, 0.2f, zOffset);
                        
                        wallNr++;
                    }

                    foreach (XmlElement wall in subroom.SelectNodes("polygon[@caption='obstacle']"))
                    {
                        ObstacleExtrudeGeometry.create(wall.GetAttribute("caption") + (" ") + wallNr, parsePoints(wall), height, zOffset);
                        wallNr++;
                    }
                    foreach (XmlElement area in subroom.SelectNodes("polygon[@caption='origin'" +
                                                                    "or @caption='destination' " +
                                                                    "or @caption='scaledArea' " +
                                                                    "or @caption='waitingZone' " +
                                                                    "or @caption='beamExit' " +
                                                                    "or @caption='eofWall' " +
                                                                    "or @caption='oben' " +
                                                                    "or @caption='unten']"))
                    {
                        AreaGeometry.create(area.GetAttribute("caption") + (" ") + wallNr, parsePoints(area));
                        wallNr++;
                    }

                    FloorExtrudeGeometry.create("floor " + floorNr, parsePoints(subroom), zOffset);
                    floorNr++;
                }
                foreach (XmlElement subroom in room.SelectNodes("subroom[@class='stair'"
                                                              + "or @class='idle_escalator']"))
                {
                    float A_x = TryParseWithDefault.ToSingle(subroom.GetAttribute("A_x"), 0);
                    float B_y = TryParseWithDefault.ToSingle(subroom.GetAttribute("B_y"), 0);
                    float C_z = TryParseWithDefault.ToSingle(subroom.GetAttribute("C_z"), 0);  //EXCEPTION: sometimes "C_z" is referred to as "C"
                    if (C_z == 0)
                    {
                        C_z = TryParseWithDefault.ToSingle(subroom.GetAttribute("C"), 0);
                    }

                    float pxUp = 0;     //Variables need to be assigned
                    float pyUp = 0;
                    float pxDown = 0;
                    float pyDown = 0;

                    foreach (XmlElement up in subroom.SelectNodes("up"))
                    {
                        pxUp = TryParseWithDefault.ToSingle(up.GetAttribute("px"), 0);
                        pyUp = TryParseWithDefault.ToSingle(up.GetAttribute("py"), 0);
                    }
                    foreach (XmlElement down in subroom.SelectNodes("down"))
                    {
                        pxDown = TryParseWithDefault.ToSingle(down.GetAttribute("px"), 0);
                        pyDown = TryParseWithDefault.ToSingle(down.GetAttribute("py"), 0);
                    }

                    bool lowerPart = true;
                    List<Vector2> coordFirst = new List<Vector2>();
                    List<Vector2> coordSecond = new List<Vector2>();
                    foreach (XmlElement stair in subroom.SelectNodes("polygon[@caption='wall']"))
                    {   
                        if (lowerPart == true)
                        {
                            coordFirst = parsePoints(stair);
                            lowerPart = false;
                        } else
                        {
                            coordSecond = parsePoints(stair);
                            lowerPart = true;
                        }
                    }
                    StairExtrudeGeometry.create(subroom.GetAttribute("class") + (" ") + stairNr, coordFirst, coordSecond, A_x, B_y, C_z, pxUp, pyUp, pxDown, pyDown);
                    stairNr++;
                } 
            }
        }

    }

    static List<Vector2> parsePoints(XmlElement polyPoints)
    {
        List<Vector2> list = new List<Vector2>();

        foreach (XmlElement vertex in polyPoints.SelectNodes("vertex"))
        {
            float x;
            float y;
            if (float.TryParse(vertex.GetAttribute("px"), out x) && float.TryParse(vertex.GetAttribute("py"), out y))
            {
                list.Add(new Vector2(x, y));
            }
        }

        foreach (XmlElement openWall in polyPoints.SelectNodes("polygon[@caption='wall'" +
                                                               "or @caption='origin'" +
                                                               "or @caption='destination' " +
                                                               "or @caption='scaledArea' " +
                                                               "or @caption='waitingZone' " +
                                                               "or @caption='beamExit' " +
                                                               "or @caption='eofWall' " +
                                                               "or @caption='oben' " +
                                                               "or @caption='unten']"))  //used to list vertices of all walls in a subroom to create the floor
        {
            foreach (XmlElement vertex in openWall.SelectNodes("vertex"))
            {
                float x;
                float y;
                if (float.TryParse(vertex.GetAttribute("px"), out x) && float.TryParse(vertex.GetAttribute("py"), out y))
                {
                    list.Add(new Vector2(x, y));
                }
            }
        }

        return list;
    }

    void loadPedestrianFile(string filename)
    {
        if (!System.IO.File.Exists(filename))
        {
            Debug.Log("Error: File " + filename + " not found.");
            return;
        }

        XmlDocument xmlDocTraj = new XmlDocument();
        xmlDocTraj.LoadXml(System.IO.File.ReadAllText(filename));

        PedestrianLoader pl = GameObject.Find("PedestrianLoader").GetComponent<PedestrianLoader>();

        XmlNode trajectories = xmlDocTraj.SelectSingleNode("//trajectories");

        int fps = 8, FrameID;  //8 is used as a default framerate. Actual value is read from xmlDocTraj

        foreach (XmlElement header in trajectories.SelectNodes("header"))
        {
            
            XDocument doc = XDocument.Load(filename);
            var fpsInput = doc.Descendants("frameRate");

            foreach (var fpsVal in fpsInput)
            {
                if (float.Parse(fpsVal.Value) != 0)
                    fps = (int) float.Parse(fpsVal.Value);
            }  
        }

        foreach (XmlElement frame in trajectories.SelectNodes("frame"))
        {
            int.TryParse(frame.GetAttribute("ID"), out FrameID);

            if (FrameID % fps == 0)
            {
                foreach (XmlElement agent in frame)
                {
                    decimal time;
                    int id;
                    float x;
                    float y;
                    float z;     
                    decimal.TryParse(frame.GetAttribute("ID"), out time);
                    int.TryParse(agent.GetAttribute("ID"), out id);
                    float.TryParse(agent.GetAttribute("x"), out x);
                    float.TryParse(agent.GetAttribute("y"), out y);
                    float.TryParse(agent.GetAttribute("z"), out z);
                    pl.addPedestrianPosition(new PedestrianPosition(id, time / fps, x, y, z));
                }
            }
        }
        pl.createPedestrians();
    }
}