using System;
using System.Web;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using CSL.Web;
using CSL.Web.Models;
using DocumentFormat.OpenXml.Office2010.Excel;
using TMS.Web.Areas.Identity.Data;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TMS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using CSL.Web;
using TMS.Models;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using TMS.Web.Controllers;
using Microsoft.AspNetCore.Http;
using System.Runtime.Intrinsics.Arm;
using static System.Net.Mime.MediaTypeNames;
using Image = iTextSharp.text.Image;

namespace TMS.Web.Models
{
    public class TankTicketPdfRenderer
    {
        private Font fontTitle;
        private Font fontSubTitle;
        private Font fontFieldLabel;
        private Font fontFieldValue;
        private Font fontTableHeader;
        private Font fontTableValue;

        private const string formatDate = "MMM/dd/yyyy";
        private const string formatApprovalDate = "MMM/dd/yyyy HH:mm:ss";
        //private string plantCode = "1129";


        public TankTicketPdfRenderer()
        {
            fontTitle = new Font(Font.HELVETICA, 12, Font.BOLD);
            fontSubTitle = new Font(Font.HELVETICA, 11, Font.BOLD, BaseColor.Blue);
            fontFieldLabel = new Font(Font.HELVETICA, 9, Font.BOLD);
            fontFieldValue = new Font(Font.HELVETICA, 9, Font.NORMAL);
            fontTableHeader = new Font(Font.HELVETICA, 7, Font.BOLD);
            fontTableValue = new Font(Font.HELVETICA, 7, Font.NORMAL);
        }

        public byte[] Render(TankTicket tankTicket, string logoPath, int plantCode)
        {
            //PrepareCustomFields(svrc);

            Document doc = new Document(PageSize.A4);
            doc.SetMargins(36, 36, 18, 36);
            using (MemoryStream ms = new MemoryStream())
            {
                PdfWriter.GetInstance(doc, ms);
                doc.Open();
                doc.AddTitle(string.Format("Tank Ticket ID # {0}", tankTicket.Id));
                doc.AddCreator("TMS WEB");

                PdfPTable mainTable = new PdfPTable(2);
                mainTable.WidthPercentage = 100;
                RenderContent(mainTable, tankTicket, logoPath, plantCode);
                doc.Add(mainTable);
                doc.Close();
                return ms.ToArray();
            }
        }
        public byte[] RenderAll(List<TankTicket> tankTickets, string logoPath, int plantCode)
        {
            //PrepareCustomFields(svrc);

            Document doc = new Document(PageSize.A4);
            doc.SetMargins(36, 36, 18, 36);
            using (MemoryStream ms = new MemoryStream())
            {
                PdfWriter.GetInstance(doc, ms);
                doc.Open();
                doc.AddTitle(string.Format("Tank Ticket All print"));
                doc.AddCreator("TMS WEB");

                PdfPTable mainTable = new PdfPTable(2);
                mainTable.WidthPercentage = 100;
                RenderAllContent(mainTable, tankTickets, logoPath, plantCode);
                doc.Add(mainTable);
                doc.Close();
                return ms.ToArray();
            }
        }

        private void RenderHeader(PdfPTable mainTable, string logoPath)
        {
            PdfPCell imgCell = new PdfPCell();
            imgCell.Border = Rectangle.BOTTOM_BORDER;
            imgCell.BorderWidthBottom = 2;
            Image logo = Image.GetInstance(logoPath);
            logo.ScalePercent(50);
            imgCell.AddElement(logo);
            mainTable.AddCell(imgCell);

            PdfPCell cell = new PdfPCell();
            cell.Border = Rectangle.BOTTOM_BORDER;
            cell.BorderWidthBottom = 2;
            Paragraph tIFPOnline = new Paragraph("FDM WEB", fontTitle);
            tIFPOnline.Alignment = Element.ALIGN_RIGHT;
            cell.AddElement(tIFPOnline);
            Paragraph tSvrc = new Paragraph("TANK TICKET", fontTitle);
            tSvrc.Alignment = Element.ALIGN_RIGHT;
            cell.AddElement(tSvrc);

            mainTable.AddCell(cell);

        }



        private void RenderContent(PdfPTable mainTable,TankTicket tankTicket, string logoPath, int plantCode)
        {
            PdfPCell leftContent = new PdfPCell();
            //leftContent.Border = 0;
            leftContent.PaddingRight = 10;
            leftContent.PaddingLeft = 10;
            leftContent.PaddingTop = 10;
            leftContent.Border = Rectangle.RIGHT_BORDER | Rectangle.LEFT_BORDER | Rectangle.BOTTOM_BORDER | Rectangle.TOP_BORDER;
            RenderLocationPertamina(leftContent, tankTicket, tankTicket.tank, null, logoPath, plantCode);
               
            mainTable.AddCell(leftContent);
            RenderNullContent(mainTable);
                
            //if (i == total && total % 2 == 1)
            //{
            //    RenderRightContent(mainTable, tankTicket, tank, product, logoPath);
            //}
            //    }
        }
        private void RenderAllContent(PdfPTable mainTable, List<TankTicket> tickets, string logoPatch, int plantCode)
        {
            int totalTicket = tickets.Count();
            int cnt = 1;
            foreach(TankTicket tankTicket in tickets)
            {
                PdfPCell content = new PdfPCell();
                //leftContent.Border = 0;
                content.PaddingRight = 10;
                content.PaddingLeft = 10;
                content.PaddingTop = 10;
                content.Border = Rectangle.RIGHT_BORDER | Rectangle.LEFT_BORDER | Rectangle.BOTTOM_BORDER | Rectangle.TOP_BORDER;
                RenderLocationPertamina(content, tankTicket, tankTicket.tank, null, logoPatch, plantCode);
                mainTable.AddCell(content);
                if(cnt==totalTicket&& totalTicket % 2 == 1)
                {
                    RenderNullContent(mainTable);
                }
                cnt++;
            }
        }

        private void RenderNullContent(PdfPTable mainTable)
        {
            PdfPCell nullContent = new PdfPCell();
            //rightContent.Border = Rectangle.RIGHT_BORDER | Rectangle.LEFT_BORDER | Rectangle.BOTTOM_BORDER | Rectangle.TOP_BORDER;
            nullContent.Border = 0;
            nullContent.PaddingLeft = 10;
            nullContent.PaddingRight = 10;
            nullContent.PaddingTop = 10;
            //RenderForOther(rightContent, tankTicket, tank, product, logoPath);
            mainTable.AddCell(nullContent);
        }

        private void RenderLeftContent1(PdfPTable mainTable, TankTicket tankTicket, Tank tank, Product product, string logoPath)
        {
            PdfPCell leftContent = new PdfPCell();
            leftContent.Border = Rectangle.RIGHT_BORDER | Rectangle.LEFT_BORDER | Rectangle.BOTTOM_BORDER | Rectangle.TOP_BORDER;
            leftContent.PaddingRight = 10;
            leftContent.PaddingLeft = 10;
            leftContent.PaddingTop = 10;
            //RenderLocationPertamina(leftContent);
            RenderForUser(leftContent, tankTicket, tank, product, logoPath);
            mainTable.AddCell(leftContent);
        }


       

        private void RenderRightContent1(PdfPTable mainTable, TankTicket tankTicket, Tank tank, Product product, string logoPath)
        {
            PdfPCell rightContent = new PdfPCell();
            rightContent.Border = Rectangle.RIGHT_BORDER | Rectangle.LEFT_BORDER | Rectangle.BOTTOM_BORDER | Rectangle.TOP_BORDER;
            rightContent.PaddingLeft = 10;
            rightContent.PaddingRight = 10;
            rightContent.PaddingTop = 10;
            //RenderForAnother(rightContent);
            RenderForTransportir(rightContent, tankTicket, tank, product, logoPath);
            mainTable.AddCell(rightContent);
        }

        //private string StatusReservasiLookup(TankTicket tankTicket)
        //{
        //    string toReturn = "";
        //    if (data.StatusReservasi == 1)
        //    {
        //        toReturn = "PSC";
        //    }
        //    else if (data.StatusReservasi == 2)
        //    {
        //        toReturn = "Routine Dipping";
        //    }
        //    else if (data.StatusReservasi == 3)
        //    {
        //        toReturn = "Intertanks Transfer(TT)";
        //    }
        //    else if (data.StatusReservasi == 4)
        //    {
        //        toReturn = "Receiving Other (ROT)";
        //    }
        //    else if (data.StatusReservasi == 5)
        //    {
        //        toReturn = "Sales (ILS)";
        //    }
        //    else if (data.StatusReservasi == 6)
        //    {
        //        toReturn = "Physical Inventory (PI)";
        //    }
        //    else if (data.StatusReservasi == 7)
        //    {
        //        toReturn = "STOCK CHECK (CHK)";
        //    }
        //    else if (data.StatusReservasi == 8)
        //    {
        //        toReturn = "Upgrade/Downgrade (UDG)";
        //    }
        //    else if (data.StatusReservasi == 9)
        //    {
        //        toReturn = "Blending(BLD)";
        //    }

        //    return toReturn;
        //}

        //private string ProductLookup(TankTicket tankTicket)
        //{
        //    //string toReturn = "";
        //    //FDM.Models.TankLiveDataHelper tank = TankLiveDataHelper.FindById(tankTicket.TankNumber);

        //    //toReturn = tank.ProductName;

        //    //return toReturn;
        //}

        //private Tank TankProperties(TankTicket data)
        //{
        //    Tank toReturn = new FDM.Models.Tank();

        //    toReturn = Tank.FindById(data.TankNumber);

        //    return toReturn;
        //}

        private void RenderLocationPertamina(PdfPCell leftCell, TankTicket tankTicket, Tank tank, Product product, string logoPath, int plantCode)
        {

            //string statusReservasi = StatusReservasiLookup(tankTicket);
            //string product = ProductLookup(svrc);
            //Tank tankData = TankProperties(svrc);
            try
            {
                DateTime ticketTimestamps = Convert.ToDateTime(tankTicket.Timestamp);
                string PlantCode = plantCode.ToString();
                //calculate level product
                double level = (double)((int)(tankTicket.LiquidLevel ?? 0));
                double waterLevel = (double)((int)(tankTicket.WaterLevel ?? 0));
                level = level - waterLevel;

                var label = FontFactory.GetFont("Arial", 8, Font.BOLD);
                var value = FontFactory.GetFont("Arial", 8, Font.NORMAL);

                Image logo = Image.GetInstance(logoPath);
                logo.ScalePercent(30);
                logo.Alignment = Element.ALIGN_LEFT;
                leftCell.AddElement(logo);

                Paragraph tHeader = new Paragraph("TANK TICKET", fontTitle);
                tHeader.Leading = 2;
                tHeader.SpacingBefore = 20;
                tHeader.SpacingAfter = 10;
                tHeader.Alignment = Element.ALIGN_CENTER;
                leftCell.AddElement(tHeader);


                PdfPTable tInner = new PdfPTable(2);
                tInner.SpacingBefore = 5;
                tInner.WidthPercentage = 100;
                tInner.SetWidths(new float[] { 4, 6 });


                PdfPCell formLabel = new PdfPCell();
                PdfPCell formValue = new PdfPCell();
                formLabel = new PdfPCell(new Phrase("Plant Code", label));
                tInner.AddCell(formLabel);
                formValue = new PdfPCell(new Phrase(PlantCode, value));
                tInner.AddCell(formValue);
                formLabel = new PdfPCell(new Phrase("Ticket No", label));
                tInner.AddCell(formLabel);
                PdfPCell cell = new PdfPCell(new Phrase(tankTicket.Ticket_Number, new Font(Font.TIMES_ROMAN, 8f)));
                tInner.AddCell(cell);
                formLabel = new PdfPCell(new Phrase("Tank Ticket Type", label));
                tInner.AddCell(formLabel);
                formValue = new PdfPCell(new Phrase(tankTicket.Operation_Type, value));
                tInner.AddCell(formValue);
                formLabel = new PdfPCell(new Phrase("Operation Status", label));
                tInner.AddCell(formLabel);
                formValue = new PdfPCell(new Phrase((tankTicket.Operation_Status != 1) ? "Close" : "Open", value));
                tInner.AddCell(formValue);


                leftCell.AddElement(tInner);


                //element for date
                PdfPTable DateContainer = new PdfPTable(4);
                DateContainer.WidthPercentage = 100;
                DateContainer.SetWidths(new float[] { 4, 2, 2, 2 });

                formLabel = new PdfPCell(new Phrase("", label));
                DateContainer.AddCell(formLabel);
                formLabel = new PdfPCell(new Phrase("Day", label));
                formLabel.HorizontalAlignment = 1;
                DateContainer.AddCell(formLabel);
                formLabel = new PdfPCell(new Phrase("Month", label));
                formLabel.HorizontalAlignment = 1;
                DateContainer.AddCell(formLabel);
                formLabel = new PdfPCell(new Phrase("Year", label));
                formLabel.HorizontalAlignment = 1;
                DateContainer.AddCell(formLabel);
                formLabel = new PdfPCell(new Phrase("Date", label));
                DateContainer.AddCell(formLabel);
                formValue = new PdfPCell(new Phrase(String.Format("{0:dd}", ticketTimestamps), value));
                formValue.HorizontalAlignment = 1;
                DateContainer.AddCell(formValue);
                formValue = new PdfPCell(new Phrase(String.Format("{0:MMM}", ticketTimestamps), value));
                formValue.HorizontalAlignment = 1;
                DateContainer.AddCell(formValue);
                formValue = new PdfPCell(new Phrase(String.Format("{0:yyyy}", ticketTimestamps), value));
                formValue.HorizontalAlignment = 1;
                DateContainer.AddCell(formValue);
                leftCell.AddElement(DateContainer);

                //PdfPCell date = new PdfPCell(new Phrase(String.Format("{0:d MMM yyyy HH:mm:ss}", ticketTimestamps), new Font(Font.FontFamily.TIMES_ROMAN, 8f)));
                //date.Border = 0;
                //tInner.AddCell(date);

                PdfPTable tInner2 = new PdfPTable(2);
                tInner2.WidthPercentage = 100;
                tInner2.SetWidths(new float[] { 4, 6 });

                formLabel = new PdfPCell(new Phrase("Hour", label));
                tInner2.AddCell(formLabel);
                formValue = new PdfPCell(new Phrase(String.Format("{0:HH:mm:ss}", ticketTimestamps), value));
                tInner2.AddCell(formValue);
                formLabel = new PdfPCell(new Phrase("Tank Name", label));
                tInner2.AddCell(formLabel);
                formValue = new PdfPCell(new Phrase(tank.Tank_Name.ToString(), value));
                tInner2.AddCell(formValue);
                formLabel = new PdfPCell(new Phrase("Product Name", label));
                tInner2.AddCell(formLabel);
                formValue = new PdfPCell(new Phrase(product.Product_Name.ToString(), value));
                tInner2.AddCell(formValue);
                leftCell.AddElement(tInner2);


                PdfPTable tInner1 = new PdfPTable(3);

                tInner1.WidthPercentage = 100;
                tInner1.SetWidths(new float[] { 4, 5, 1 });
                formLabel = new PdfPCell(new Phrase("Height of Tank ", label));
                tInner1.AddCell(formLabel);
                formValue = new PdfPCell(new Phrase(tank.Tank_Height.ToString(), value));
                formValue.HorizontalAlignment = 2;
                tInner1.AddCell(formValue);
                formLabel = new PdfPCell(new Phrase("mm", label));
                formLabel.HorizontalAlignment = 1;
                tInner1.AddCell(formLabel);

                formLabel = new PdfPCell(new Phrase("Tape", label));
                tInner1.AddCell(formLabel);
                formValue = new PdfPCell(new Phrase(tank.Tape.ToString(), value));
                //formValue = new PdfPCell(new Phrase(tankData.TankBaseHeight.ToString(), value));
                formValue.HorizontalAlignment = 2;
                tInner1.AddCell(formValue);
                formLabel = new PdfPCell(new Phrase("mm", label));
                formLabel.HorizontalAlignment = 1;
                tInner1.AddCell(formLabel);

                formLabel = new PdfPCell(new Phrase("BOB", label));
                tInner1.AddCell(formLabel);
                formValue = new PdfPCell(new Phrase(tank.Bob.ToString(), value));
                //formValue = new PdfPCell(new Phrase(tankData.PointDeskHeight.ToString(), value));
                formValue.HorizontalAlignment = 2;
                tInner1.AddCell(formValue);
                formLabel = new PdfPCell(new Phrase("mm", label));
                formLabel.HorizontalAlignment = 1;
                tInner1.AddCell(formLabel);

                formLabel = new PdfPCell(new Phrase("Blank", label));
                tInner1.AddCell(formLabel);
                formValue = new PdfPCell(new Phrase(" ", value));
                //formValue = new PdfPCell(new Phrase(tankData.PointDeskHeight.ToString(), value));
                formValue.HorizontalAlignment = 2;
                tInner1.AddCell(formValue);
                formLabel = new PdfPCell(new Phrase("mm", label));
                formLabel.HorizontalAlignment = 1;
                tInner1.AddCell(formLabel);

                formLabel = new PdfPCell(new Phrase("Product + Water ", label));
                tInner1.AddCell(formLabel);
                formValue = new PdfPCell(new Phrase((level + tankTicket.WaterLevel).ToString(), value));
                formValue.HorizontalAlignment = 2;
                tInner1.AddCell(formValue);
                formLabel = new PdfPCell(new Phrase("mm", label));
                formLabel.HorizontalAlignment = 1;
                tInner1.AddCell(formLabel);


                formLabel = new PdfPCell(new Phrase("Water ", label));
                tInner1.AddCell(formLabel);
                formValue = new PdfPCell(new Phrase(tankTicket.WaterLevel.ToString(), value));
                formValue.HorizontalAlignment = 2;
                tInner1.AddCell(formValue);
                formLabel = new PdfPCell(new Phrase("mm", label));
                formLabel.HorizontalAlignment = 1;
                tInner1.AddCell(formLabel);

                formLabel = new PdfPCell(new Phrase("Product ", label));
                tInner1.AddCell(formLabel);
                formValue = new PdfPCell(new Phrase(level.ToString(), value));
                formValue.HorizontalAlignment = 2;
                tInner1.AddCell(formValue);
                formLabel = new PdfPCell(new Phrase("mm", label));
                formLabel.HorizontalAlignment = 1;
                tInner1.AddCell(formLabel);

                formLabel = new PdfPCell(new Phrase("Temperature", label));
                tInner1.AddCell(formLabel);
                formValue = new PdfPCell(new Phrase((tankTicket.LiquidTemperature.HasValue ? tankTicket.LiquidTemperature.Value.ToString("F") : "0"), value));
                formValue.HorizontalAlignment = 2;
                tInner1.AddCell(formValue);
                formLabel = new PdfPCell(new Phrase("'C", label));
                formLabel.HorizontalAlignment = 1;
                tInner1.AddCell(formLabel);

                formLabel = new PdfPCell(new Phrase("Density", label));
                tInner1.AddCell(formLabel);
                formValue = new PdfPCell(new Phrase((tankTicket.LiquidDensity.HasValue ? tankTicket.LiquidDensity.Value.ToString("0.0000") : "0.0000"), value));
                formValue.HorizontalAlignment = 2;
                tInner1.AddCell(formValue);
                formLabel = new PdfPCell(new Phrase("gr/cm3", label));
                formLabel.HorizontalAlignment = 1;
                tInner1.AddCell(formLabel);

                leftCell.AddElement(tInner1);




                PdfPTable tPetugas = new PdfPTable(3);
                tPetugas.SpacingBefore = 30;
                tPetugas.WidthPercentage = 100;
                tPetugas.SetWidths(new float[] { 1, 1, 1 });
                tPetugas.AddCell(CreateFieldLabel("SUPERVISOR"));
                tPetugas.AddCell(CreateFieldLabel("TANK GAUGER"));
                tPetugas.AddCell(CreateFieldLabel("CUSTOMS"));
                tPetugas.AddCell(CreateFieldLabel(""));
                tPetugas.AddCell(CreateFieldValue(""));
                tPetugas.AddCell(CreateFieldValue(""));
                tPetugas.AddCell(CreateFieldLabel(""));
                tPetugas.AddCell(CreateFieldValue(""));
                tPetugas.AddCell(CreateFieldValue(""));
                tPetugas.AddCell(CreateFieldLabel(""));
                tPetugas.AddCell(CreateFieldValue(""));
                tPetugas.AddCell(CreateFieldValue(""));
                tPetugas.AddCell(CreateFieldLabel(""));
                tPetugas.AddCell(CreateFieldValue(""));
                tPetugas.AddCell(CreateFieldValue(""));
                tPetugas.AddCell(CreateFieldLabel(""));
                tPetugas.AddCell(CreateFieldValue(""));
                tPetugas.AddCell(CreateFieldValue(""));
                tPetugas.AddCell(CreateFieldLabel(""));
                tPetugas.AddCell(CreateFieldValue(""));
                tPetugas.AddCell(CreateFieldValue(""));
                tPetugas.AddCell(CreateFieldLabel(".........."));
                tPetugas.AddCell(CreateFieldValue(".........."));
                tPetugas.AddCell(CreateFieldValue(".........."));
                leftCell.AddElement(tPetugas);
            }
            catch(Exception ex)
            {
                throw new Exception(ex.Message);
            }
            


        }

        private void RenderForOther(PdfPCell leftCell, TankTicket tankTicket, Tank tank, Product product, string logoPath)
        {
            //TankTicket newTankTicket = new TankTicket();
            //svrc = data;
            //string statusReservasi = StatusReservasiLookup(svrc);
            //string product = ProductLookup(svrc);
            //Tank tankData = TankProperties(svrc);
            DateTime ticketTimestamps = Convert.ToDateTime(tankTicket.Timestamp);

            //calculate level product
            double level = (double)((int)(tankTicket.LiquidLevel ?? 0));
            double waterLevel = (double)((int)(tankTicket.WaterLevel ?? 0));
            level = level - waterLevel;

            var label = FontFactory.GetFont("Arial", 8, Font.BOLD);
            var value = FontFactory.GetFont("Arial", 8, Font.NORMAL);

            Image logo = Image.GetInstance(logoPath);
            logo.ScalePercent(30);
            logo.Alignment = Element.ALIGN_LEFT;
            leftCell.AddElement(logo);

            Paragraph tHeader = new Paragraph("TANK TICKET", fontTitle);
            tHeader.Leading = 2;
            tHeader.SpacingBefore = 20;
            tHeader.SpacingAfter = 10;
            tHeader.Alignment = Element.ALIGN_CENTER;
            leftCell.AddElement(tHeader);

            string operationMode = "";
            if (tankTicket.Operation_Status == 1)
            {
                operationMode = "Automatic";
            }
            else
            {
                operationMode = "Manual";
            }


            PdfPTable tInner = new PdfPTable(2);
            tInner.SpacingBefore = 5;
            tInner.WidthPercentage = 100;
            tInner.SetWidths(new float[] { 4, 6 });

            PdfPCell formLabel = new PdfPCell(new Phrase("Tank Ticket Type", label));
            tInner.AddCell(formLabel);
            PdfPCell formValue = new PdfPCell(new Phrase(tankTicket.Operation_Type, value));
            tInner.AddCell(formValue);
            formLabel = new PdfPCell(new Phrase("Operation Status", label));
            tInner.AddCell(formLabel);
            formValue = new PdfPCell(new Phrase(tankTicket.Operation_Status.ToString(), value));
            tInner.AddCell(formValue);
            formLabel = new PdfPCell(new Phrase("Operation Mode", label));
            tInner.AddCell(formLabel);
            formValue = new PdfPCell(new Phrase(operationMode, value));
            tInner.AddCell(formValue);
            formLabel = new PdfPCell(new Phrase("Ticket No", label));
            tInner.AddCell(formLabel);
            PdfPCell cell = new PdfPCell(new Phrase(tankTicket.Ticket_Number, new Font(Font.TIMES_ROMAN, 8f)));
            tInner.AddCell(cell);
            leftCell.AddElement(tInner);


            //element for date
            PdfPTable DateContainer = new PdfPTable(4);
            DateContainer.WidthPercentage = 100;
            DateContainer.SetWidths(new float[] { 4, 2, 2, 2 });

            formLabel = new PdfPCell(new Phrase("", label));
            DateContainer.AddCell(formLabel);
            formLabel = new PdfPCell(new Phrase("Day", label));
            formLabel.HorizontalAlignment = 1;
            DateContainer.AddCell(formLabel);
            formLabel = new PdfPCell(new Phrase("Month", label));
            formLabel.HorizontalAlignment = 1;
            DateContainer.AddCell(formLabel);
            formLabel = new PdfPCell(new Phrase("Year", label));
            formLabel.HorizontalAlignment = 1;
            DateContainer.AddCell(formLabel);
            formLabel = new PdfPCell(new Phrase("Date", label));
            DateContainer.AddCell(formLabel);
            formValue = new PdfPCell(new Phrase(String.Format("{0:dd}", ticketTimestamps), value));
            formValue.HorizontalAlignment = 1;
            DateContainer.AddCell(formValue);
            formValue = new PdfPCell(new Phrase(String.Format("{0:MMM}", ticketTimestamps), value));
            formValue.HorizontalAlignment = 1;
            DateContainer.AddCell(formValue);
            formValue = new PdfPCell(new Phrase(String.Format("{0:yyyy}", ticketTimestamps), value));
            formValue.HorizontalAlignment = 1;
            DateContainer.AddCell(formValue);
            leftCell.AddElement(DateContainer);

            //PdfPCell date = new PdfPCell(new Phrase(String.Format("{0:d MMM yyyy HH:mm:ss}", ticketTimestamps), new Font(Font.FontFamily.TIMES_ROMAN, 8f)));
            //date.Border = 0;
            //tInner.AddCell(date);

            PdfPTable tInner2 = new PdfPTable(2);
            tInner2.WidthPercentage = 100;
            tInner2.SetWidths(new float[] { 4, 6 });

            formLabel = new PdfPCell(new Phrase("Hour", label));
            tInner2.AddCell(formLabel);
            formValue = new PdfPCell(new Phrase(String.Format("{0:HH:mm:ss}", ticketTimestamps), value));
            tInner2.AddCell(formValue);
            formLabel = new PdfPCell(new Phrase("Tank Number", label));
            tInner2.AddCell(formLabel);
            formValue = new PdfPCell(new Phrase(tank.TankId.ToString(), value));
            tInner2.AddCell(formValue);
            formLabel = new PdfPCell(new Phrase("Product Name", label));
            tInner2.AddCell(formLabel);
            formValue = new PdfPCell(new Phrase(product.Product_Name.ToString(), value));
            tInner2.AddCell(formValue);
            leftCell.AddElement(tInner2);


            PdfPTable tInner1 = new PdfPTable(3);

            tInner1.WidthPercentage = 100;
            tInner1.SetWidths(new float[] { 4, 5, 1 });
            formLabel = new PdfPCell(new Phrase("Height of Tank ", label));
            tInner1.AddCell(formLabel);
            formValue = new PdfPCell(new Phrase(tank.Tank_Height.ToString(), value));
            formValue.HorizontalAlignment = 2;
            tInner1.AddCell(formValue);
            formLabel = new PdfPCell(new Phrase("mm", label));
            formLabel.HorizontalAlignment = 1;
            tInner1.AddCell(formLabel);

            formLabel = new PdfPCell(new Phrase("Tape ", label));
            tInner1.AddCell(formLabel);
            formValue = new PdfPCell(new Phrase(tank.Tape.ToString(), value));
            formValue.HorizontalAlignment = 2;
            tInner1.AddCell(formValue);
            formLabel = new PdfPCell(new Phrase("mm", label));
            formLabel.HorizontalAlignment = 1;
            tInner1.AddCell(formLabel);

            formLabel = new PdfPCell(new Phrase("BOB ", label));
            tInner1.AddCell(formLabel);
            formValue = new PdfPCell(new Phrase(tank.Bob.ToString(), value));
            formValue.HorizontalAlignment = 2;
            tInner1.AddCell(formValue);
            formLabel = new PdfPCell(new Phrase("mm", label));
            formLabel.HorizontalAlignment = 1;
            tInner1.AddCell(formLabel);

            formLabel = new PdfPCell(new Phrase("Ullage ", label));
            tInner1.AddCell(formLabel);
            formValue = new PdfPCell(new Phrase((Convert.ToDouble(tank.Height_Safe_Capacity) - (tankTicket.LiquidLevel ?? 0)).ToString(), value));
            formValue.HorizontalAlignment = 2;
            tInner1.AddCell(formValue);
            formLabel = new PdfPCell(new Phrase("mm", label));
            formLabel.HorizontalAlignment = 1;
            tInner1.AddCell(formLabel);

            formLabel = new PdfPCell(new Phrase("Product + Water ", label));
            tInner1.AddCell(formLabel);
            formValue = new PdfPCell(new Phrase(((tankTicket.LiquidLevel ?? 0) + (tankTicket.WaterLevel ?? 0)).ToString(), value));
            formValue.HorizontalAlignment = 2;
            tInner1.AddCell(formValue);
            formLabel = new PdfPCell(new Phrase("mm", label));
            formLabel.HorizontalAlignment = 1;
            tInner1.AddCell(formLabel);


            formLabel = new PdfPCell(new Phrase("Water ", label));
            tInner1.AddCell(formLabel);
            formValue = new PdfPCell(new Phrase(tankTicket.WaterLevel.ToString(), value));
            formValue.HorizontalAlignment = 2;
            tInner1.AddCell(formValue);
            formLabel = new PdfPCell(new Phrase("mm", label));
            formLabel.HorizontalAlignment = 1;
            tInner1.AddCell(formLabel);

            formLabel = new PdfPCell(new Phrase("Product ", label));
            tInner1.AddCell(formLabel);
            formValue = new PdfPCell(new Phrase(level.ToString(), value));
            formValue.HorizontalAlignment = 2;
            tInner1.AddCell(formValue);
            formLabel = new PdfPCell(new Phrase("mm", label));
            formLabel.HorizontalAlignment = 1;
            tInner1.AddCell(formLabel);

            formLabel = new PdfPCell(new Phrase("Temperature", label));
            tInner1.AddCell(formLabel);
            formValue = new PdfPCell(new Phrase((tankTicket.LiquidTemperature.HasValue ? tankTicket.LiquidTemperature.Value.ToString("F") : "0"), value));
            formValue.HorizontalAlignment = 2;
            tInner1.AddCell(formValue);
            formLabel = new PdfPCell(new Phrase("'C", label));
            formLabel.HorizontalAlignment = 1;
            tInner1.AddCell(formLabel);

            formLabel = new PdfPCell(new Phrase("Density", label));
            tInner1.AddCell(formLabel);
            formValue = new PdfPCell(new Phrase((tankTicket.LiquidDensity.HasValue ? tankTicket.LiquidDensity.Value.ToString("0.0000") : "0.0000"), value));
            formValue.HorizontalAlignment = 2;
            tInner1.AddCell(formValue);
            formLabel = new PdfPCell(new Phrase("gr/cm3", label));
            formLabel.HorizontalAlignment = 1;
            tInner1.AddCell(formLabel);

            leftCell.AddElement(tInner1);




            PdfPTable tPetugas = new PdfPTable(3);
            tPetugas.SpacingBefore = 30;
            tPetugas.WidthPercentage = 100;
            tPetugas.SetWidths(new float[] { 1, 1, 1 });
            tPetugas.AddCell(CreateFieldLabel("SUPERVISOR"));
            tPetugas.AddCell(CreateFieldLabel("TANK GAUGER"));
            tPetugas.AddCell(CreateFieldLabel("CUSTOMS"));
            tPetugas.AddCell(CreateFieldLabel(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldLabel(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldLabel(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldLabel(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldLabel(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldLabel(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldLabel(".........."));
            tPetugas.AddCell(CreateFieldValue(".........."));
            tPetugas.AddCell(CreateFieldValue(".........."));
            leftCell.AddElement(tPetugas);

        }

        private void RenderForUser(PdfPCell leftCell, TankTicket tankTicket, Tank tank, Product product, string logoPath)
        {

            string statusReservasi = tankTicket.Operation_Type; // Operation Type

            //calculate level product
            double level = (double)((int)(tankTicket.LiquidLevel ?? 0));
            double waterLevel = (double)((int)(tankTicket.WaterLevel ?? 0));
            level = level - waterLevel;

            Image logo = Image.GetInstance(logoPath);
            logo.ScalePercent(30);
            logo.Alignment = Element.ALIGN_LEFT;
            leftCell.AddElement(logo);

            Paragraph tHeader = new Paragraph("TANK TICKET", fontTitle);
            tHeader.Leading = 2;
            tHeader.SpacingBefore = 20;
            tHeader.SpacingAfter = 10;
            tHeader.Alignment = Element.ALIGN_CENTER;
            leftCell.AddElement(tHeader);

            PdfPTable tInner = new PdfPTable(2);
            tInner.SpacingBefore = 5;
            tInner.WidthPercentage = 100;
            tInner.SetWidths(new float[] { 3, 5 });
            tInner.AddCell(CreateFieldLabel("Reservation"));
            tInner.AddCell(CreateFieldValue(statusReservasi));
            tInner.AddCell(CreateFieldLabel("Ticket No"));
            tInner.AddCell(CreateFieldValue(tankTicket.Ticket_Number.ToString()));
            tInner.AddCell(CreateFieldLabel("Date "));
            tInner.AddCell(CreateFieldValue(tankTicket.Timestamp.ToString()));
            tInner.AddCell(CreateFieldLabel("Tank Number"));
            tInner.AddCell(CreateFieldValue(tank.TankId.ToString()));
            tInner.AddCell(CreateFieldLabel("Product Name"));
            tInner.AddCell(CreateFieldValue(product.Product_Name));
            leftCell.AddElement(tInner);

            PdfPTable tInner1 = new PdfPTable(3);

            tInner1.WidthPercentage = 100;
            tInner1.SetWidths(new float[] { 3, 4, 1 });
            tInner1.AddCell(CreateFieldLabel("Height of Tank "));
            tInner1.AddCell(CreateFieldValue(tank.Tank_Height.ToString()));
            tInner1.AddCell(CreateFieldValue("mm"));
            tInner1.AddCell(CreateFieldLabel("Tape "));
            //tInner1.AddCell(CreateFieldValue(tank.HeightTankBase.ToString()));
            tInner1.AddCell(CreateFieldValue("mm"));
            tInner1.AddCell(CreateFieldLabel("BOB "));
            //tInner1.AddCell(CreateFieldValue(tank.HeightPointDesk.ToString()));
            tInner1.AddCell(CreateFieldValue("mm"));
            tInner1.AddCell(CreateFieldLabel("BLANK "));
            //tInner1.AddCell(CreateFieldValue(tank.DeadstockVolume.ToString()));
            tInner1.AddCell(CreateFieldValue("mm"));
            tInner1.AddCell(CreateFieldLabel("Product + Water "));
            tInner1.AddCell(CreateFieldValue(((tankTicket.LiquidLevel ?? 0) + (tankTicket.WaterLevel ?? 0)).ToString()));
            tInner1.AddCell(CreateFieldValue("mm"));
            tInner1.AddCell(CreateFieldLabel("Water"));
            tInner1.AddCell(CreateFieldValue((tankTicket.WaterLevel ?? 0).ToString()));
            tInner1.AddCell(CreateFieldValue("mm"));
            tInner1.AddCell(CreateFieldLabel("Product"));
            tInner1.AddCell(CreateFieldValue(level.ToString()));
            tInner1.AddCell(CreateFieldValue("mm"));
            tInner1.AddCell(CreateFieldLabel("Temperature"));
            tInner1.AddCell(CreateFieldValue((tankTicket.LiquidTemperature ?? 0).ToString()));
            tInner1.AddCell(CreateFieldValue("'C"));
            tInner1.AddCell(CreateFieldLabel("Density"));
            tInner1.AddCell(CreateFieldValue((tankTicket.LiquidDensity ?? 0).ToString()));
            tInner1.AddCell(CreateFieldValue("Kg/L"));
            leftCell.AddElement(tInner1);




            PdfPTable tPetugas = new PdfPTable(3);
            tPetugas.SpacingBefore = 30;
            tPetugas.WidthPercentage = 100;
            tPetugas.SetWidths(new float[] { 1, 1, 1 });
            tPetugas.AddCell(CreateFieldLabel("SUPERVISOR"));
            tPetugas.AddCell(CreateFieldLabel("TANK GAUGER"));
            tPetugas.AddCell(CreateFieldLabel("CUSTOMS"));
            tPetugas.AddCell(CreateFieldLabel(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldLabel(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldLabel(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldLabel(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldLabel(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldLabel(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldLabel(".........."));
            tPetugas.AddCell(CreateFieldValue(".........."));
            tPetugas.AddCell(CreateFieldValue(".........."));
            leftCell.AddElement(tPetugas);

        }

        private void RenderForTransportir(PdfPCell leftCell, TankTicket tankTicket, Tank tank, Product product, string logoPath)
        {

            string statusReservasi = tankTicket.Operation_Type; // Operation Type
            //calculate level product 
            double level = (double)(tankTicket.LiquidLevel ?? 0);
            double waterLevel = (double)(tankTicket.WaterLevel ?? 0);
            level = level - waterLevel;


            Image logo = Image.GetInstance(logoPath);
            logo.ScalePercent(30);
            logo.Alignment = Element.ALIGN_LEFT;
            leftCell.AddElement(logo);


            Paragraph tHeader = new Paragraph("TANK TICKET", fontTitle);
            tHeader.Leading = 2;
            tHeader.SpacingBefore = 20;
            tHeader.SpacingAfter = 10;
            tHeader.Alignment = Element.ALIGN_CENTER;
            leftCell.AddElement(tHeader);





            PdfPTable tInner = new PdfPTable(2);
            tInner.SpacingBefore = 5;
            tInner.WidthPercentage = 100;
            tInner.SetWidths(new float[] { 3, 5 });
            tInner.AddCell(CreateFieldLabel("Reservation"));
            tInner.AddCell(CreateFieldValue(statusReservasi));
            tInner.AddCell(CreateFieldLabel("Ticket No"));
            tInner.AddCell(CreateFieldValue(tankTicket.Ticket_Number.ToString()));
            tInner.AddCell(CreateFieldLabel("Date "));
            tInner.AddCell(CreateFieldValue(tankTicket.Timestamp.ToString()));
            tInner.AddCell(CreateFieldLabel("Tank Number"));
            tInner.AddCell(CreateFieldValue(tank.TankId.ToString()));
            tInner.AddCell(CreateFieldLabel("Product Name"));
            tInner.AddCell(CreateFieldValue(product.Product_Name));
            leftCell.AddElement(tInner);

            PdfPTable tInner1 = new PdfPTable(3);

            tInner1.WidthPercentage = 100;
            tInner1.SetWidths(new float[] { 3, 4, 1 });
            tInner1.AddCell(CreateFieldLabel("Height of Tank "));
            tInner1.AddCell(CreateFieldValue(tank.Tank_Height.ToString()));
            tInner1.AddCell(CreateFieldValue("mm"));
            tInner1.AddCell(CreateFieldLabel("Tape "));
            tInner1.AddCell(CreateFieldValue(""));
            tInner1.AddCell(CreateFieldValue("mm"));
            tInner1.AddCell(CreateFieldLabel("BOB "));
            tInner1.AddCell(CreateFieldValue(""));
            tInner1.AddCell(CreateFieldValue("mm"));
            tInner1.AddCell(CreateFieldLabel("Ullage "));
            tInner1.AddCell(CreateFieldValue((tank.Height_Safe_Capacity - (tankTicket.LiquidLevel ?? 0)).ToString()));
            tInner1.AddCell(CreateFieldValue("mm"));
            tInner1.AddCell(CreateFieldLabel("Product + Water "));
            tInner1.AddCell(CreateFieldValue(((tankTicket.LiquidLevel ?? 0) + (tankTicket.WaterLevel ?? 0)).ToString()));
            tInner1.AddCell(CreateFieldValue("mm"));
            tInner1.AddCell(CreateFieldLabel("Water"));
            tInner1.AddCell(CreateFieldValue((tankTicket.WaterLevel ?? 0).ToString()));
            tInner1.AddCell(CreateFieldValue("mm"));
            tInner1.AddCell(CreateFieldLabel("Product"));
            tInner1.AddCell(CreateFieldValue(level.ToString()));
            tInner1.AddCell(CreateFieldValue("mm"));
            tInner1.AddCell(CreateFieldLabel("Temperature"));
            tInner1.AddCell(CreateFieldValue((tankTicket.LiquidTemperature ?? 0).ToString()));
            tInner1.AddCell(CreateFieldValue("'C"));
            tInner1.AddCell(CreateFieldLabel("Density"));
            tInner1.AddCell(CreateFieldValue((tankTicket.LiquidDensity ?? 0).ToString()));
            tInner1.AddCell(CreateFieldValue("Kg/L"));
            leftCell.AddElement(tInner1);




            PdfPTable tPetugas = new PdfPTable(3);
            tPetugas.SpacingBefore = 30;
            tPetugas.WidthPercentage = 100;
            tPetugas.SetWidths(new float[] { 1, 1, 1 });
            tPetugas.AddCell(CreateFieldLabel("SUPERVISOR"));
            tPetugas.AddCell(CreateFieldLabel("TANK GAUGER"));
            tPetugas.AddCell(CreateFieldLabel("CUSTOMS"));
            tPetugas.AddCell(CreateFieldLabel(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldLabel(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldLabel(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldLabel(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldLabel(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldLabel(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldValue(""));
            tPetugas.AddCell(CreateFieldLabel(".........."));
            tPetugas.AddCell(CreateFieldValue(".........."));
            tPetugas.AddCell(CreateFieldValue(".........."));
            leftCell.AddElement(tPetugas);


        }


        private PdfPCell CreateFieldValue(string text)
        {
            PdfPCell p = new PdfPCell(new Phrase(text, fontFieldValue));
            p.Border = Rectangle.NO_BORDER;
            return p;
        }

        private PdfPCell CreateFieldLabel(string text)
        {
            PdfPCell p = new PdfPCell(new Phrase(text, fontFieldLabel));
            p.Border = Rectangle.NO_BORDER;
            return p;
        }
    }
}
