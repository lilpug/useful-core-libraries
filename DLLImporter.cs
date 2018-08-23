using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;

/// <summary>
/// This class allows your to import DLLs at runtime and run their functions
/// Note: can use re-pack to combine .net framework and .net core libraries into a single DLL for importing
/// </summary>
public static class DLLImporter
{
    public static object Run(string url, string className, string functionName, params object[] functionParameters)
    {
        //Downloads the DLL file from the URL supplied
        byte[] file;
        using (WebClient wc = new WebClient())
        {
            file = wc.DownloadData(url);
        }

        //If the file failed to download then it throws an exception rather than continue
        if (file == null)
        {
            throw new Exception("The DLL was not obtained from the URL");
        }

        //Loads the assembly found
        Assembly DLL = Assembly.Load(file);

        //Gets the class we are going to be using from the DLL
        Type myType = DLL.GetType(className);

        //Gets the function we are going to be running from the class
        MethodInfo myMethod = myType.GetMethod(functionName);

        //Creates an instance of the class
        var obj = Activator.CreateInstance(myType);

        //Runs the function and returns data back
        return myMethod.Invoke(obj, functionParameters);
    }

	// This function runs a DLL from inside a zip file
	// Note: this assume the DLL is in the root of the zip file.
    public static object RunFromZipFile(string url, string dllFileName, string className, string functionName, params object[] functionParameters)
    {
        var files = DownloadZip(url, dllFileName);

        //If the file failed to download then it throws an exception rather than continue
        if (files == null || files.Count == 0)
        {
            throw new Exception("The DLL was not obtained from the URL");
        }
        
        //Loads the assembly found
        Assembly DLL = Assembly.Load(files.First().Value);
        
        //Gets the class we are going to be using from the DLL
        Type myType = DLL.GetType(className);

        //Gets the function we are going to be running from the class
        MethodInfo myMethod = myType.GetMethod(functionName);

        //Creates an instance of the class
        var obj = Activator.CreateInstance(myType);
        
        //Runs the function and returns data back
        return myMethod.Invoke(obj, functionParameters);
    }

    private static Dictionary<string, byte[]> DownloadZip(string url, string dllFileName)
    {
        //Downloads the zip file from the URL
        byte[] downloadedZipFile;
        using (WebClient wc = new WebClient())
        {
            downloadedZipFile = wc.DownloadData(url);
        }

        //Loops through the zip file and extracts only the zip files
        return GetZipFiles(downloadedZipFile, dllFileName);
    }

    private static Dictionary<string, byte[]> GetZipFiles(byte[] zippedBuffer, string dllFileName)
    {
        Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();

        //Loads the zip file byte array into a memory stream so we can access the content
        using (MemoryStream stream = new MemoryStream(zippedBuffer))
        {
            using (ZipArchive arc = new ZipArchive(stream))
            {
                //Linq query that filters all the files we do not want via their extension types
                var zipFileFilter = from file in arc.Entries
                                    //Checks the file extension exists and is at the end of the full filename
                                    //Note: helps to stop things like abc.dll.old
                                    let extensionIndex = file.Name.IndexOf(".dll")
                                    //Used to check if the entire filename exists
                                    let mainIndex = file.Name.IndexOf($"{dllFileName}.dll")
                                    where mainIndex != -1 && extensionIndex != -1 && extensionIndex == file.Name.Length - ".dll".Length
                                    select file;

                //Checks we have results before continuing
                if (zipFileFilter != null && zipFileFilter.FirstOrDefault() != null)
                {
                    //Hard casts it for optimization
                    var fileEntries = zipFileFilter.ToArray();

                    //Will need to filter here that only DLLs!
                    foreach (var file in fileEntries)
                    {
                        string filename = file.Name;
                        byte[] fileData = new byte[file.Length];

                        //uncompresses and reads the file entry in the zip file
                        using (Stream uncompressedFile = file.Open())
                        {
                            //Creates a temporary memory stream for us to write the byte array
                            using (MemoryStream filestream = new MemoryStream())
                            {
                                //Copies the data of the zip entry into our stream
                                uncompressedFile.CopyTo(filestream);

                                //Takes the bytes out of the stream and puts it into our byte array
                                fileData = filestream.ToArray();
                            }
                        }

                        //Puts the file data into our dictionary
                        files.Add(filename, fileData);
                    }
                }
            }
        }

        return files;
    }
}
