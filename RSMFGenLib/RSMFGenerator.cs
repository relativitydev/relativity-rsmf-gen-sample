using System;
using System.IO;
using System.IO.Compression;
using MimeKit;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using MimeKit.Text;
using Relativity.RSMFU.Validator.Interfaces;
using Relativity.RSMFU.Validator;

/* ----------------------------------------------------------------------------
 * <copyright file="RSMFGenerator.cs" company="Relativity ODA LLC">
 *  © Relativity All Rights Reserved.
 * </copyright>
 *----------------------------------------------------------------------------
*/

namespace RSMFGenLib
{
    /* 
     * The RSMFGenerator class contains sample code for how to take an rsmf_manifest.json file and convert it into a fully formed RSMF file.
     * It expects that the rsmf_manifest.json file lives in a directory with any attachments that it references.  This directory is specified
     * through the inputDirectory parameter of the GenerateRSMF method.  The resultant RSMF file is specified through the outputFile parameter.
     * 
     * There are three layers to an RSMF file.  The json layer (rsmf_manifest.json), the zip layer (a zip that contains the rsmf_manifest.json
     * and any attachments it references), and the RSMF layer, sometimes also referred to as the EML layer.  
     * 
     * This sample code leverages two open source libraries to generate the RSMF level.  It uses NewtonSoft.JSON to parse the rsmf_manifest.json
     * file and map the resultant data to fields at the RSMF layer.  It also uses the MimeKit library to construct the final RSMF file.  These
     * libraries were chosen for their ease of use and their feature sets, but they are by no means the only way to accomplish these tasks.
     * 
     * The zip layer is handled by the built in .Net ZipArchive library.  As with the open source libraries, this isn't the only way to accomplish
     * the task of building a zip file, but its function and ease of use aligned well for this sample code.
     */
    public class RSMFGenerator
    {
        /* The Generator property is used to populate the "X-RSMF-Generator EML field.*/
        public string Generator { get; set; } = "Relativity RSMF Generator Sample Library";
        /* The Custodian properties are used to populate the "From" EML field.*/
        public string CustodianDisplay { get; set; } = string.Empty;
        public string CustodianEmail { get; set; }
        public bool ValidateZip { get; set; } = false;


        /* 
         * Convert the contents of the supplied directory into an RSMF file that can be ingested into Relativity via
         * Relativity processing.  The inputDirectory must contain a file named rsmf_manifest.json.  Any other files
         * in the directory will also be included in the RSMF regardless of if they are referenced by the
         * manifest files or not.  The outputFile will be where the RSMF file is written to.
         */
        public void GenerateRSMF(DirectoryInfo inputDirectory, FileInfo outputFile)
        {
            FileInfo manifest = null;
            if (!inputDirectory.Exists)
            {
                throw new Exception($"The input directory {inputDirectory.FullName} doesn't exist.");
            }
            
            if (!File.Exists(Path.Combine(inputDirectory.FullName, "rsmf_manifest.json")))
            {
                throw new Exception($"The file rsmf_manifest.json does not exist in {inputDirectory.FullName}.");
            }
            manifest = inputDirectory.GetFiles("rsmf_manifest.json", SearchOption.TopDirectoryOnly)?[0];

            /* 
             * Create a stream to a zip that contains the rsmf_manifest.json file and any other files that exist in its
             * directory.
             */
            using (Stream rsmf_zip = CreateRSMFZip(inputDirectory))
            {

                /*
                 * Validating the zip stream validates that the JSON in the rsmf_manifest.json file is compliant with the
                 * RSMF specification.  It also verifies that the attachment references in rsmf_manifest.json have equivalent
                 * files in the Zip.
                 */
                if(ValidateZip)
                {
                    IValidator zipValidator = RSMFValidatorFactory.GetRSMFZipValidator();
                    RSMFValidatorResult results = zipValidator.PerformTests(rsmf_zip);
                    /*
                     * This code only reports errors, not warnings.  Warnings from the validator are
                     * contained in the results.Warnings list.
                     */
                    if(results.Result == ResultTypes.Failed)
                    {
                        StringBuilder errorString = new StringBuilder();
                        foreach(IValidatorError error in results.Errors)
                        {
                            errorString.AppendLine();
                            errorString.AppendLine(error.ErrorMessage);                            
                        }                        
                        throw new Exception("Validation of the generated ZIP failed: " + errorString.ToString());
                    }
                }
                
                /* Leverage MimeKit to create the representation of the RSMF file.*/
                MimeMessage rsmf = CreateRSMF(manifest);
                /* MimePart defaults to application/octet-stream, which is exactly what is necessary.*/
                MimePart attachment = new MimePart();
                attachment.Content = new MimeContent(rsmf_zip);
                attachment.ContentDisposition = new ContentDisposition(ContentDisposition.Attachment);
                /* The RSMF specification requires that the name of the attachment be rsmf.zip */
                attachment.FileName = "rsmf.zip";
                /*
                 * This is a trick to make it easier to add the zip attachment to the RSMF.  It is known that the Body property
                 * of RSMF returned by CreateRSMF is a Multipart object, so this cast is safe.
                 */
                ((Multipart)rsmf.Body).Add(attachment);
                /*
                 * Prepare the final EML before writing it out.  This accomplishes various things like making sure the body
                 * text is encoded correctly.  For RSMF files, the encoding constraint is 7-bit.
                */
                rsmf.Prepare(EncodingConstraint.SevenBit);
                /* Finally, write the object to the provided file. */
                rsmf.WriteTo(outputFile.FullName);
            }
            
        }

        /* 
         * Creating the EML layer of an RSMF file can be a little tricky, but MimeKit alleviates a lot of the issues.  This method
         * returns a MimeMessage object that contains required and optional headers for the RSMF EML layer.  It also constructs
         * a body for the RSMF that contains data pulled from the rsmf_manifest.json file.
         */ 
        private MimeMessage CreateRSMF(FileInfo manifest)
        {
            MimeMessage rsmf = new MimeMessage();            
            /*The generator isn't necessary, but can help to pinpoint issues with RSMF files.  This can be any string.*/
            rsmf.Headers.Add("X-RSMF-Generator", Generator);
            /* 
             * Currently in Relativity only the From field of the RSMF has name normilization applied to it.  This field can contain any
             * name, but realistically it should contain the name and email address of the custodian of the RSMF source data.  If the
             * rsmf_manifest.json contains specific information on who the custodian of the data is, then it could be obtained from there.
             */
            if (CustodianEmail != null)                
            {                   
                rsmf.From.Add(new MailboxAddress(CustodianDisplay, CustodianEmail));
            }
            else
            {
                /*Without this information, it is not necessary to write out the From field.*/
                rsmf.Headers.Remove(HeaderId.From);
            }
            
            /*The following headers are not required in the RSMF file.*/
            rsmf.Headers.Remove(HeaderId.Subject);
            rsmf.Headers.Remove(HeaderId.MessageId);
            rsmf.Headers.Remove(HeaderId.Date);
            /*This ends the headers that can be generated without information from the rsmf_manifest.json file.*/

            /* 
             * Using the information from rsmf_manifest, other headers can be populated.  The message body can also be populated
             * at the same time.
             */
            using (StreamReader reader = manifest.OpenText())
            using (JsonReader jsonTextReader = new JsonTextReader(reader))
            {
                /*
                 * The RSMF headers for begin/end dates expect the date format to be in ISO date format (rfc3339).  To accomplish this NewtonSoft
                 * needs to be told not to convert the date strings from the JSON (which are in ISO format) to a pretty format.
                 */
                jsonTextReader.DateParseHandling = DateParseHandling.None;
                JObject json = (JObject)JToken.ReadFrom(jsonTextReader);

                /* This is the only required header for an RSMF file.  All others are optional.*/
                rsmf.Headers.Add("X-RSMF-Version", json["version"].ToString());
                /*
                 * The To field of the RSMF should be populated with the participants of the rsmf_manifest.json file.
                 * Order isn't really important here.
                 */
                if (json["participants"] != null)
                {
                    JToken participants = json["participants"];
                    /*
                     * It is much faster to create a List and send all of the addresses at once
                     * to the MimeMessage object.
                     */
                    List<MailboxAddress> to = new List<MailboxAddress>();
                    foreach (JObject p in participants)
                    {
                        string display = p["display"]?.ToString() ?? "";
                        string email = p["email"]?.ToString() ?? "";
                        to.Add(new MailboxAddress(display, email));
                    }
                    rsmf.To.AddRange(to);
                }

                TextPart textPart = new TextPart(TextFormat.Plain);
                if (json["events"] != null)
                {
                    /* To sort the events they need to be in a List.*/
                    List<JToken> events = json["events"].ToList();

                    /* The list of events gets sorted by their timestamp so that older event data is put into the body text first.*/
                    events.Sort((first, second) =>
                    {
                        string firstTimestamp = first["timestamp"]?.ToString() ?? "";
                        string secondTimestamp = second["timestamp"]?.ToString() ?? "";
                        return string.Compare(firstTimestamp, secondTimestamp);
                    });

                    /*These are optional headers, but adding them populates the relevant fields in Relativity.*/
                    rsmf.Headers.Add("X-RSMF-EventCount", events.Count.ToString());
                    if (events.Count > 1)
                    {
                        if (events[0]["timestamp"] != null)
                        {
                            rsmf.Headers.Add("X-RSMF-BeginDate", events[0]["timestamp"].ToString());
                        }
                        if (events.Last()["timestamp"] != null)
                        {
                            rsmf.Headers.Add("X-RSMF-EndDate", events.Last()["timestamp"].ToString());
                        }

                    }
                    textPart.Text = BuildBody(json, events);
                }
                else
                {
                    /*Create an empty body.*/
                    textPart.Text = "";
                }
                /*
                 * In MimeKit, the Body property is the body of the whole EML.  Since the RSMF requires rsmf.zip as an attachment, the body needs to be multipart/mixed.
                 */
                Multipart multipart = new Multipart("mixed");
                multipart.Add(textPart);
                rsmf.Body = multipart;
            }
            return rsmf;
        }

        /*
         * Data from events is used to populate the body of the RSMF file.  When the RSMF is processed using Relativity Processing, the body text is used to
         * populate the extracted text field of the document object.  Any event information can be used to populate the body text.  Event information can be 
         * translated to EML body text in any order, but the sorted event data is going to be used to more closely align dtSearch proximity search hits to the
         * Relativity Viewer proximity search hits.  The data from events that is written out also closely matches the searchable data in the Relativity RSMF
         * viewer.  There are a few examples in this code that are commented out.  They have been presented  here to show how to add more data to the body
         * text than is strictly necessary.
         */
        private string BuildBody(JObject json, List<JToken> events)
        {
            StringBuilder body = new StringBuilder();
            foreach (JToken e in events)
            {
                /*
                 * This commented out block adds the conversation title to the body text.  It is not necessary for search hits, but if a customer finds
                 * it useful it can be added.
                 */                
                /*if (e["conversation"] != null && json["conversations"] != null)
                {
                    var c = json["conversations"].FirstOrDefault(co => co["id"] != null && co["display"] != null && co["id"].ToString() == e["conversation"].ToString());
                    if (c != null)
                    {
                        body.AppendLine();
                        body.AppendLine($"Conversation:      {c["display"].ToString()} ");
                        body.AppendLine();
                    }
                }*/
                /*This adds the event participant to the body text.*/
                if (e["participant"] != null && json["participants"] != null)
                {
                    /* Find the first participant definition that has an id that matches the participant of the event.*/
                    JToken p = json["participants"].FirstOrDefault(pa => pa["id"] != null && pa["display"] != null && pa["id"].ToString() == e["participant"].ToString());
                    if (p != null)
                    {
                        body.AppendLine(p["display"].ToString());
                        body.AppendLine();
                    }
                }
                /* 
                 * This commented out block adds the timestamp of the event to the body text.  It is not necessary for search hits, but if a customer finds
                 * it useful it can be added.
                 */
                /*if (e["timestamp"] != null)
                {
                    DateTime et = DateTime.Parse(e["timestamp"].ToString(), null, System.Globalization.DateTimeStyles.RoundtripKind);
                    body.AppendLine($"{et.ToShortDateString()} {et.ToShortTimeString()}");
                    body.AppendLine();
                }
                */
                if (e["body"] != null)
                {
                    body.AppendLine(e["body"].ToString());
                }
                body.AppendLine();
                /*This adds the event's reactions to the body text.*/
                if(e["reactions"] != null)
                {
                    foreach(JToken r in e["reactions"])
                    {
                        if(r["value"] != null)
                        {
                            body.AppendLine(r["value"].ToString());
                            body.AppendLine();
                        }
                    }
                }
                body.AppendLine();
            }
            return body.ToString();
        }

        /*
         * The rsmf.zip consists of the rsmf_manifest.json file and all relevant attachments and avatars.  .Net has a built in
         * ZipArchive class that can be used to easily create this file.  There are a few methods, the one implemented below
         * is done in memory.  Other options are to use the ZipFile.CreateFromDirectory method
         * (https://docs.microsoft.com/en-us/dotnet/api/system.io.compression.zipfile.createfromdirectory?view=netframework-4.6.2)
         * and use a temporary file to hold the zip before adding it as an attachment to the RSMF EML file.
         */
        private Stream CreateRSMFZip(DirectoryInfo inputDirectory)
        {
            MemoryStream zipStream = new MemoryStream();
            
            using (ZipArchive zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                foreach(var file in inputDirectory.EnumerateFiles())
                {
                    zipArchive.CreateEntryFromFile(file.FullName, file.Name);
                }                
            }
            return zipStream;
        }
    }
}
