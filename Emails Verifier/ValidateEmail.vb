Imports System.Text.RegularExpressions
Imports System.Net
Imports System.Net.NetworkInformation

Imports System.Net.Sockets

Imports Bdev.Net.Dns

Public Class VerifyEmail

    Public Shared Function ValidateAddress(ByVal eMailAddress As String) As Boolean

        Return Regex.IsMatch(eMailAddress, "^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,4}$", RegexOptions.IgnoreCase)

    End Function

    Private Shared Function GetMailDomain(ByVal eMailAddress As String) As String

        Dim re As New Regex("^(?<Name>[A-Z0-9._%+-]+)@(?<domain>[A-Z0-9.-]+\.[A-Z]{2,4})$", RegexOptions.IgnoreCase)

        Dim strDomain As String = String.Empty

        If re.IsMatch(eMailAddress) Then
            strDomain = re.Match(eMailAddress).Groups("domain").Value
        End If

        Return strDomain

    End Function

    Public Shared Function ValidateDomain(ByVal eMailAddress As String)

        If MXRecordLookup(eMailAddress).Count > 0 Then
            Return True
        Else
            Return False
        End If

    End Function

    Private Shared Function ListMyDNS() As List(Of IPAddress)

        Dim myDNS As New List(Of IPAddress)

        Dim int As NetworkInformation.NetworkInterface() = Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces
        For Each i As NetworkInterface In int
            If i.OperationalStatus = OperationalStatus.Up Then
                Console.WriteLine("DNS")
                For Each a As IPAddress In i.GetIPProperties.DnsAddresses
                    myDNS.Add(a)
                Next
            End If
        Next

        Return myDNS

    End Function

    Public Shared Function MXRecordLookup(ByVal eMailAddress As String) As List(Of String)

        Dim lstServers As New List(Of String)

        ' Try each server in order until we get hits...
        For Each dnsServerAddress As IPAddress In ListMyDNS()

            Dim request As Request = New Request()

            ' add the question
            request.AddQuestion(New Question(GetMailDomain(eMailAddress), DnsType.MX, DnsClass.IN))
            Try
                ' send the query and collect the response
                Dim response As Response = Resolver.Lookup(request, dnsServerAddress)
         
                ' iterate through all the answers and add the servers to the list
                For Each answer As Answer In response.Answers

                    Dim record As MXRecord = DirectCast(answer.Record, MXRecord)
                    lstServers.Add(record.DomainName)
                Next
            Catch
            End Try

            ' Once we have servers stop
            If lstServers.Count > 0 Then Exit For
        Next

        Return lstServers

    End Function

    Public Shared Function VerifyAddress(ByVal eMailAddress As String) As Boolean

        Dim bVerified As Boolean = False

        Dim lstMailServers As List(Of String)

        If ValidateAddress(eMailAddress) Then
            lstMailServers = MXRecordLookup(eMailAddress)

            For Each server As String In lstMailServers
                bVerified = QueryServerForMailBox(server, eMailAddress)
                If bVerified Then Exit For
            Next

        End If

        Return bVerified

    End Function

    Private Shared Function QueryServerForMailBox(ByVal Server As String, ByVal MailBox As String) As Boolean

        Dim comm As NetworkTransaction = Nothing

        Dim strDomain As String = GetMailDomain(MailBox)
        Dim strResponse As String

        Dim bAddressExists As Boolean = False

        Try
            ' Connect to the server
            comm = New NetworkTransaction()
            comm.Delimiter = ControlChars.CrLf

            If comm.Connect(Server, 25) Then

                ' Read initial response from server
                strResponse = comm.ReadLine

                ' establish comms with server
                strResponse = comm.Transact("HELO mydomain.com")

                ' Specify sender
                strResponse = comm.Transact("MAIL FROM:<verifyaddress@mydomain.com>")

                ' Specify recipient
                strResponse = comm.Transact("RCPT TO:<" + MailBox + ">")
                If strResponse.StartsWith("250") Then bAddressExists = True
            End If

        Catch ex As Exception
            Console.WriteLine(ex.ToString)
        Finally
            If Not comm Is Nothing Then comm.Disconnect()
        End Try

        Return bAddressExists

    End Function

End Class
