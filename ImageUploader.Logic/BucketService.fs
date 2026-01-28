namespace ImageUploader.Logic

open Amazon.S3
open Amazon.S3.Model
open System.IO
open System.Xml.Linq
open System.Threading.Tasks
open Operations

module BucketService =


    [<Literal>]
    let BucketName = "my-fsharp-image-gallery"

    [<Literal>]
    let BucketUrl = "https://my-fsharp-image-gallery.s3.amazonaws.com"

    [<Literal>]
    let xmlKey = "index.xml"



    let addNewDocFile (fileName: string) (doc: XDocument) =
        doc.Root.Add(
            XElement(
                XName.Get "File",
                XElement(XName.Get "Name", fileName),
                XElement(XName.Get "Link", $"{BucketUrl}/images/{fileName}"),
                XElement(XName.Get "UploadDate", System.DateTime.UtcNow.ToString("o")) // Example of adding a third info
            )
        )

        doc

    let updateXmlMetadata (s3: #IAmazonS3) (newFileName: string) =
        task {


            let! getResult = s3.GetObjectAsync(BucketName, xmlKey) |> Task.catch

            let xmlDoc =

                match getResult with
                | Ok response ->
                    use reader = new StreamReader(response.ResponseStream)
                    XDocument.Parse(reader.ReadToEnd())
                | Error _ -> XDocument(XElement(XName.Get "Files"))


            let updatedDoc = xmlDoc |> addNewDocFile newFileName

            use ms = new MemoryStream()
            updatedDoc.Save(ms)
            ms.Position <- 0L

            let putReq =
                PutObjectRequest(BucketName = BucketName, Key = xmlKey, InputStream = ms)

            return! s3.PutObjectAsync(putReq)


        }

    let uploadImage (s3: #IAmazonS3) (fileName: string) (content: Stream) =
        task {

            let request =
                PutObjectRequest(BucketName = BucketName, Key = $"images/{fileName}", InputStream = content)

            let! _ = s3.PutObjectAsync(request)


            let! result = updateXmlMetadata s3 fileName

            return result

        }





    let deleteImage (s3: #IAmazonS3) (fileName: string) =

        task {
            let deleteReq =
                DeleteObjectRequest(BucketName = BucketName, Key = $"images/{fileName}")

            let! _ = s3.DeleteObjectAsync(deleteReq)

            let! getResult = s3.GetObjectAsync(BucketName, xmlKey) |> Task.catch

            return!
                match getResult with
                | Error _ -> Task.FromResult false
                | Ok response ->
                    use reader = new StreamReader(response.ResponseStream)
                    let xmlDoc = XDocument.Parse(reader.ReadToEnd())

                    xmlDoc.Descendants(XName.Get "File")
                    |> Seq.tryFind (fun el ->
                        let nameTag = el.Element(XName.Get "Name")
                        nameTag <> null && (nameTag.Value = fileName))
                    |> function
                        | Some el ->
                            el.Remove()
                            use ms = new MemoryStream()
                            xmlDoc.Save(ms)
                            ms.Position <- 0L

                            // We return a task here, which return! will unwrap for the caller
                            task {
                                let! _ =
                                    s3.PutObjectAsync(
                                        PutObjectRequest(BucketName = BucketName, Key = xmlKey, InputStream = ms)
                                    )

                                return true
                            }
                        | None -> Task.FromResult false
        }
