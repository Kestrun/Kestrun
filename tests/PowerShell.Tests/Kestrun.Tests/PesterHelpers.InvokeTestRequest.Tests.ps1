param()
BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
}

Describe 'Invoke-TestRequest timeout replay gating' {
    It 'retries once on timeout for GET requests' {
        $script:callCount = 0
        Mock Invoke-WebRequest {
            $script:callCount++
            if ($script:callCount -eq 1) {
                throw [System.Threading.Tasks.TaskCanceledException]::new('operation was canceled')
            }

            return [pscustomobject]@{
                StatusCode = 200
                Content = 'ok'
            }
        }

        $response = Invoke-TestRequest -Uri 'http://localhost/example' -Method Get
        $response.StatusCode | Should -Be 200
        $script:callCount | Should -Be 2
    }

    It 'does not retry on timeout for POST requests by default' {
        $script:callCount = 0
        Mock Invoke-WebRequest {
            $script:callCount++
            throw [System.Threading.Tasks.TaskCanceledException]::new('operation was canceled')
        }

        {
            Invoke-TestRequest -Uri 'http://localhost/example' -Method Post
        } | Should -Throw -ExpectedMessage '*operation was canceled*'

        $script:callCount | Should -Be 1
    }

    It 'retries once on timeout for POST when RetryOnTimeout is specified' {
        $script:callCount = 0
        Mock Invoke-WebRequest {
            $script:callCount++
            if ($script:callCount -eq 1) {
                throw [System.Threading.Tasks.TaskCanceledException]::new('operation was canceled')
            }

            return [pscustomobject]@{
                StatusCode = 201
                Content = 'created'
            }
        }

        $response = Invoke-TestRequest -RetryOnTimeout -Uri 'http://localhost/example' -Method Post
        $response.StatusCode | Should -Be 201
        $script:callCount | Should -Be 2
    }
}
