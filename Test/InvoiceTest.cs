﻿using FluentAssertions;
using Xunit;

namespace Recurly.Test
{
    public class InvoiceTest : BaseTest
    {
        [Fact]
        public void GetInvoice()
        {
            var account = CreateNewAccountWithBillingInfo();

            var adjustment = account.NewAdjustment("USD", 5000, "Test Charge");
            adjustment.Create();

            var invoice = account.InvoicePendingCharges();
            Assert.Equal("usst", invoice.TaxType);
            Assert.Equal(0.0875M, invoice.TaxRate.Value);

            var fromService = Invoices.Get(invoice.InvoiceNumber);

            invoice.Should().Be(fromService);
        }

        [Fact]
        public void GetInvoicePdf()
        {
            var account = CreateNewAccount();

            var adjustment = account.NewAdjustment("USD", 5000, "Test Charge");
            adjustment.Create();

            var invoice = account.InvoicePendingCharges();

            var pdf = invoice.GetPdf();

            pdf.Should().NotBeEmpty();
        }

        [Fact]
        public void AdjustmentAggregationInAnInvoice()
        {
            var account = CreateNewAccount();

            var adjustment = account.NewAdjustment("USD", 5000, "Test Charge");
            adjustment.Create();

            adjustment = account.NewAdjustment("USD", 5000, "Test Charge 2");
            adjustment.Create();

            adjustment = account.NewAdjustment("USD", -2500, "Test Credit");
            adjustment.Create();

            var invoice = account.InvoicePendingCharges();

            invoice.State.Should().Be(Invoice.InvoiceState.Open);
            invoice.TotalInCents.Should().Be(7500);
        }

        [Fact]
        public void MarkSuccessful()
        {
            var account = CreateNewAccount();

            var adjustment = account.NewAdjustment("USD", 3999, "Test Charge");
            adjustment.Create();

            var invoice = account.InvoicePendingCharges();

            invoice.MarkSuccessful();

            Assert.Equal(1, invoice.Adjustments.Count);

            invoice.State.Should().Be(Invoice.InvoiceState.Collected);
        }

        [Fact]
        public void FailedCollection()
        {
            var account = CreateNewAccountWithBillingInfo();

            var adjustment = account.NewAdjustment("USD", 3999, "Test Charge");
            adjustment.Create();

            var invoice = account.InvoicePendingCharges();
            invoice.MarkFailed();
            invoice.State.Should().Be(Invoice.InvoiceState.Failed);
            Assert.NotNull(invoice.ClosedAt);
        }

        [Fact]
        public void RefundSingle()
        {
            var account = CreateNewAccountWithBillingInfo();

            var adjustment = account.NewAdjustment("USD", 3999, "Test Charge");
            adjustment.Create();

            var invoice = account.InvoicePendingCharges();

            invoice.MarkSuccessful();

            invoice.State.Should().Be(Invoice.InvoiceState.Collected);

            Assert.Equal(1, invoice.Adjustments.Count);

            // refund
            var refundInvoice = invoice.Refund(adjustment, false);
            Assert.NotEqual(invoice.Uuid, refundInvoice.Uuid);
            Assert.Equal(-3999, refundInvoice.SubtotalInCents);
            Assert.Equal(1, refundInvoice.Adjustments.Count);
            Assert.Equal(-1, refundInvoice.Adjustments[0].Quantity);
            Assert.Equal(0, refundInvoice.Transactions.Count);

            account.Close();
        }

        [Fact]
        public void RefundMultiple()
        {
            var account = CreateNewAccountWithBillingInfo();

            var adjustment1 = account.NewAdjustment("USD", 1, "Test Charge 1");
            adjustment1.Create();

            var adjustment2 = account.NewAdjustment("USD", 2, "Test Charge 2", 2);
            adjustment2.Create();

            var invoice = account.InvoicePendingCharges();
            invoice.MarkSuccessful();

            Assert.Equal(2, invoice.Adjustments.Count);

            // refund
            var refundInvoice = invoice.Refund(invoice.Adjustments);
            Assert.NotEqual(invoice.Uuid, refundInvoice.Uuid);
            Assert.Equal(-5, refundInvoice.SubtotalInCents);
            Assert.Equal(2, refundInvoice.Adjustments.Count);
            Assert.Equal(-1, refundInvoice.Adjustments[0].Quantity);
            Assert.Equal(-2, refundInvoice.Adjustments[1].Quantity);

            account.Close();
        }
    }
}
